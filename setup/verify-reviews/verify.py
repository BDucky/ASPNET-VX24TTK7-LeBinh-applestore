"""Live check of product reviews (use case 32) in a real browser, against the
real app and a throwaway database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5294):
  python3 setup/verify-reviews/verify.py

What it does: a customer orders (cash), is told they can review only after
delivery; the admin delivers the order through the staff pages; the customer
reviews (shown with the average, on the model and configuration pages) and
edits it; staff reply and hide it; 390px; PASS/FAIL per check.
"""
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, otp_for, rows, scalar  # noqa: E402

h = Harness("verify-reviews", 5294)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
ADMIN, ADMIN_PASSWORD = f"admin{STAMP}@example.com", f"Admin-{STAMP}"
CUSTOMER, PASSWORD = f"reviewer{STAMP}@example.com", "Password1"


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
    slug, config = rows(db, """
        select p.Slug, ov.Value from ProductVariants v join Products p on p.Id = v.ProductId
        join VariantOptions vo on vo.VariantId = v.Id join OptionTypes ot on ot.Id = vo.OptionTypeId and ot.Code = 'config'
        join OptionValues ov on ov.Id = vo.OptionValueId
        where p.Slug = 'apple-watch-11' and v.Status = 1 and v.Price is not null and v.StockQty > 3
        group by ov.Value having count(distinct v.Price) = 1 order by ov.Value limit 1""")[0]
    proc = h.start_app("Development", db, log, {"SeedAdmin__Email": ADMIN, "SeedAdmin__Password": ADMIN_PASSWORD})
    try:
        url = h.config_url(slug, config)
        product = f"/Products/{slug}"
        step(r"""
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: 'Review Writer', Phone: '0988000111', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__E__", CUSTOMER).replace("__P__", PASSWORD))
        r = step(r"""
  await fill(page, { Code: '__C__' }); await submit(page);
  await page.goto(APP + '__URL__');
  await press(page, '[data-choice-buy]');
  await page.goto(APP + '/Checkout');
  await page.fill('#AddressLine', '3 Review Street');
  await press(page, 'button[value=place]');
  const order = page.url().replace(APP, '');
  await page.goto(APP + '__PRODUCT__');
  return JSON.stringify({ order, hint: await page.locator('.pdp-reviews').innerText(), form: await page.locator('select[name=Rating]').count() });
""".replace("__C__", otp_for(log, CUSTOMER) or "").replace("__URL__", url).replace("__PRODUCT__", product))
        order = int(r["order"].split("/")[-1])
        check("before delivery: no reviews yet, no form, and a hint", "No reviews yet." in r["hint"] and "after your order with it is delivered" in r["hint"] and r["form"] == 0, r["hint"])

        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  await page.goto(APP + '/Admin/Orders/__ID__');
  await press(page, 'button[value=Confirm]');
  await page.fill('#Carrier', 'GHN'); await page.fill('#TrackingNo', 'REV-1');
  await press(page, 'button[value=Ship]');
  await press(page, 'button[value=Complete]');
  return JSON.stringify({ done: await text(page, '.cart-status') });
""".replace("__ID__", str(order)))
        check("the order was delivered", r["done"] == ["Order completed."], str(r))

        r = step(sign_in(CUSTOMER, PASSWORD) + r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.goto(APP + '__PRODUCT__');
  await page.selectOption('select[name=Rating]', '4');
  await page.fill('textarea[name=Content]', 'Comfortable and the battery lasts two days.');
  await press(page, 'button:has-text("Post review")');
  const out = { url: page.url().replace(APP, ''), status: await text(page, '.cart-status'), average: await page.locator('[data-review-average]').innerText() };
  await page.goto(APP + '__URL__');
  out.onConfig = (await page.locator('.pdp-reviews').innerText()).includes('battery lasts two days');
  await page.selectOption('select[name=Rating]', '5');
  await press(page, 'button:has-text("Update review")');
  out.edited = await page.locator('[data-review-average]').innerText();
  await page.setViewportSize({ width: 390, height: 844 });
  out.scroll = await page.evaluate(() => document.documentElement.scrollWidth);
  await page.locator('.pdp-reviews').screenshot({ path: 'reviews-390.png' });
  await page.setViewportSize({ width: 1440, height: 900 });
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__PRODUCT__", product).replace("__URL__", url))
        check("the customer posts a review and comes back to the product", r["url"] == product and r["status"] == ["Thank you for your review."] and r["average"] == "4.0", str(r))
        check("the review also shows on the configuration page", r["onConfig"])
        check("editing changes the one review (average 5.0)", r["edited"] == "5.0" and scalar(db, "select count(*) from Reviews") == 1, r["edited"])
        check("the review block fits 390px", r["scroll"] <= 390, str(r["scroll"]))
        check("no console errors", r["consoleErrors"] == [], str(r["consoleErrors"]))

        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  await page.goto(APP + '/Admin/Orders');
  await press(page, 'a[href="/Admin/Reviews"]');
  await page.fill('input[name=Reply]', 'Thank you, enjoy it!');
  await press(page, 'button:has-text("Save reply")');
  const out = { replied: await text(page, '.cart-status') };
  await page.goto(APP + '__PRODUCT__');
  out.reply = (await page.locator('.pdp-reviews').innerText()).includes('Thank you, enjoy it!');
  await page.goto(APP + '/Admin/Reviews');
  await press(page, 'button:has-text("Hide")');
  await page.goto(APP + '__PRODUCT__');
  out.hidden = await page.locator('.pdp-reviews').innerText();
  return JSON.stringify(out);
""".replace("__PRODUCT__", product))
        check("staff reply and the reply shows on the product", r["replied"] == ["Reply saved."] and r["reply"], str(r))
        check("hiding removes it from the product page", "No reviews yet." in r["hidden"], r["hidden"][:80])
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
