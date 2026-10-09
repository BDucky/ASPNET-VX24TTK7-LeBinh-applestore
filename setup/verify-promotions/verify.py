"""Live check of promotions (use case 28) in a real browser, against the real
app and a throwaway database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5298):
  python3 setup/verify-promotions/verify.py

What it does: the seeded admin starts a 10% promotion on one product from
/Admin/Promotions; the shop shows the lower price with the old one crossed
out on the model page and the configuration page (also after a colour
change); a customer's cart and a placed cash order charge the lower price
(checked in the database); an employee can open promotions but not
vouchers; deleting the promotion puts the old price back; 390px; PASS/FAIL.
"""
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, otp_for, rows, scalar  # noqa: E402

h = Harness("verify-promotions", 5298)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
ADMIN, ADMIN_PASSWORD = f"admin{STAMP}@example.com", f"Admin-{STAMP}"
CUSTOMER, PASSWORD = f"promo{STAMP}@example.com", "Password1"


def vnd(amount):
    return f"{int(amount):,}".replace(",", ".") + " VNĐ"


def sale(price):
    # DiscountMath: 10% rounded to whole dong, half away from zero.
    off = int(price * 10 // 100 + (1 if price * 10 % 100 >= 50 else 0))
    return price - off


def sign_in(email, password):
    return r"""
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
""".replace("__E__", email).replace("__P__", password)


def main():
    db, log = TMP / "app.db", TMP / "app.log"
    h.open_browser()
    migrate(db)
    # A product with a configuration sold in two colours or more, in stock.
    product_id, slug, config = rows(db, """
        select p.Id, p.Slug, ov.Value from ProductVariants v join Products p on p.Id = v.ProductId and p.Status = 1
        join VariantOptions vo on vo.VariantId = v.Id join OptionTypes ot on ot.Id = vo.OptionTypeId and ot.Code = 'config'
        join OptionValues ov on ov.Id = vo.OptionValueId
        join VariantOptions vc on vc.VariantId = v.Id join OptionTypes oc on oc.Id = vc.OptionTypeId and oc.Code = 'color'
        where v.Status = 1 and v.Price is not null and v.StockQty > 3
        group by p.Id, ov.Value having count(distinct vc.OptionValueId) > 1 order by p.SortOrder, p.Id limit 1""")[0]
    proc = h.start_app("Development", db, log, {"SeedAdmin__Email": ADMIN, "SeedAdmin__Password": ADMIN_PASSWORD})
    try:
        url = h.config_url(slug, config)
        step(r"""
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: 'Promo Buyer', Phone: '0977000111', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__E__", CUSTOMER).replace("__P__", PASSWORD))
        step(r"""
  await fill(page, { Code: '__C__' }); await submit(page);
  return JSON.stringify({});
""".replace("__C__", otp_for(log, CUSTOMER) or ""))

        # 1. The admin starts the promotion.
        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  await page.goto(APP + '/Admin/Orders');
  await press(page, 'a[href="/Admin/Promotions"]');
  await press(page, 'a[href="/Admin/Promotions/New"]');
  const fields = { code: await page.locator('[name=Code]').count(), limit: await page.locator('[name=UsageLimit]').count() };
  await page.fill('#Name', 'Live week');
  await page.selectOption('#Type', 'Percent');
  await page.fill('#Value', '10');
  await page.selectOption('#ProductIds', '__PID__');
  await press(page, 'button:has-text("Save promotion")');
  return JSON.stringify({ url: page.url().replace(APP, ''), status: await text(page, '.cart-status'), fields });
""".replace("__PID__", str(product_id)))
        check("the admin saves a promotion from the promotions page", r["status"] == ["Promotion saved."] and r["url"].startswith("/Admin/Promotions/"), str(r))
        check("the promotion form has no code or usage limit", r["fields"] == {"code": 0, "limit": 0}, str(r["fields"]))
        promotion = int(r["url"].split("/")[-1])
        check("it is stored as an automatic promotion with no code",
              rows(db, "select Kind, Code, Name from Vouchers where Id = ?", promotion) == [(1, None, "Live week")])

        # 2. The shop, as the customer.
        # By colour and region: one colour can be sold in two regions at two prices.
        prices = {(c, reg): int(float(p)) for c, reg, p in rows(db, """
            select ov.Value, rv.Value, v.Price from ProductVariants v
            join VariantOptions vc on vc.VariantId = v.Id join OptionTypes oc on oc.Id = vc.OptionTypeId and oc.Code = 'color'
            join OptionValues ov on ov.Id = vc.OptionValueId
            join VariantOptions vr on vr.VariantId = v.Id join OptionTypes orr on orr.Id = vr.OptionTypeId and orr.Code = 'region'
            join OptionValues rv on rv.Id = vr.OptionValueId
            join VariantOptions vo on vo.VariantId = v.Id join OptionTypes ot on ot.Id = vo.OptionTypeId and ot.Code = 'config'
            join OptionValues cv on cv.Id = vo.OptionValueId
            where v.ProductId = ? and cv.Value = ? and v.Status = 1 and v.Price is not null""", product_id, config)}
        r = step(sign_in(CUSTOMER, PASSWORD) + r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.goto(APP + '__URL__');
  const read = async () => ({
    color: await page.locator('[data-choice-color].is-selected').getAttribute('data-choice-color'),
    region: await page.locator('[data-choice-region].is-selected').getAttribute('data-choice-region'),
    price: (await page.locator('[data-choice-price]').textContent()).trim(),
    was: (await page.locator('[data-choice-was]').textContent()).trim(),
    wasShown: await page.locator('[data-choice-was]').isVisible(),
    promo: (await page.locator('[data-choice-promo]').textContent()).trim(),
  });
  const out = { first: await read() };
  await page.locator('[data-choice-color]:not(.is-selected)').first().click();
  out.second = await read();
  await page.goto(APP + '/Products/__SLUG__');
  out.cardWas = await page.locator('.product-grid .price-was').count();
  await page.goto(APP + '__URL__');
  await page.setViewportSize({ width: 390, height: 844 });
  out.scroll = await page.evaluate(() => document.documentElement.scrollWidth);
  await page.screenshot({ path: 'variant-390.png' });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.screenshot({ path: 'variant-1440.png' });
  out.buying = await read();
  await press(page, '[data-choice-buy]');
  out.cart = await page.locator('.cart-line-price').first().innerText();
  await page.goto(APP + '/Checkout');
  await page.fill('#AddressLine', '5 Promo Street');
  await press(page, 'button[value=place]');
  out.order = page.url().replace(APP, '');
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__URL__", url).replace("__SLUG__", slug))
        for key in ("first", "second"):
            got = r[key]
            base = prices[(got["color"], got["region"])]
            check(f"configuration page ({got['color']}): sale price, old price crossed out, promotion name",
                  got["price"] == vnd(sale(base)) and got["was"] == vnd(base) and got["wasShown"] and got["promo"] == "Live week", str(got))
        check("the model page cards show the old price crossed out", r["cardWas"] > 0, str(r["cardWas"]))
        check("at 390px the configuration page does not scroll sideways", r["scroll"] <= 390, str(r["scroll"]))
        bought = (r["buying"]["color"], r["buying"]["region"])
        check("the cart charges the sale price and shows the old one", vnd(sale(prices[bought])) in r["cart"] and vnd(prices[bought]) in r["cart"], r["cart"])
        order = int(r["order"].split("/")[-1])
        item = rows(db, "select Price from OrderItems where OrderId = ?", order)
        check("the placed order records the sale price", len(item) == 1 and int(float(item[0][0])) == sale(prices[bought]), str(item))
        check("and its total", int(float(scalar(db, "select TotalAmount from Orders where Id = ?", order))) == sale(prices[bought]))
        check("no console errors", r["consoleErrors"] == [], str(r["consoleErrors"]))

        # 3. An employee: promotions yes, vouchers no.
        with __import__("sqlite3").connect(db) as conn:
            conn.execute("update Users set Role = 1 where Email = ?", (CUSTOMER,))
        r = step(sign_in(CUSTOMER, PASSWORD) + r"""
  await page.goto(APP + '/Admin/Orders');
  const nav = await page.locator('.admin-nav a').allTextContents();
  const promos = await page.goto(APP + '/Admin/Promotions');
  const promoTitle = (await page.locator('h1').textContent()).trim();
  await page.goto(APP + '/Admin/Vouchers');
  return JSON.stringify({ nav: nav.map(s => s.trim()), promos: promos.status(), promoTitle, vouchers: page.url().replace(APP, '') });
""")
        check("an employee sees Promotions in the staff nav and can open it",
              "Promotions" in r["nav"] and "Vouchers" not in r["nav"] and r["promos"] == 200 and r["promoTitle"] == "Promotions", str(r))
        check("an employee is turned away from vouchers", r["vouchers"].startswith("/Account/AccessDenied"), r["vouchers"])

        # 4. The admin deletes it; the old price is back.
        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  await page.goto(APP + '/Admin/Promotions/__ID__');
  await press(page, 'button:has-text("Delete promotion")');
  const out = { status: await text(page, '.cart-status') };
  await page.goto(APP + '__URL__');
  out.was = await page.locator('[data-choice-was]').isVisible();
  out.price = (await page.locator('[data-choice-price]').textContent()).trim();
  out.color = await page.locator('[data-choice-color].is-selected').getAttribute('data-choice-color');
  out.region = await page.locator('[data-choice-region].is-selected').getAttribute('data-choice-region');
  return JSON.stringify(out);
""".replace("__ID__", str(promotion)).replace("__URL__", url))
        check("deleting the promotion puts the old price back",
              r["status"] == ["Promotion deleted. Orders placed during it keep the prices they were charged."] and not r["was"] and r["price"] == vnd(prices[(r["color"], r["region"])]), str(r))
        check("the order keeps the price it was charged", int(float(rows(db, "select Price from OrderItems where OrderId = ?", order)[0][0])) == sale(prices[bought]))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
