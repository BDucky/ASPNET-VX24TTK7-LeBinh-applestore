"""Live check of the cart (use cases 11-14) in a real browser, against the
real app and a throwaway database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5288):
  python3 setup/verify-cart/verify.py

What it does:
  1. Builds a fresh database, picks a real configuration with several
     colours, and sets its stock so the limits are known (3 in one colour,
     sold out in another). Registers a customer through the real form.
  2. Visitor: the product page offers a sign-in link that follows the chosen
     colour and comes back to it after signing in.
  3. Signed in: the form posts the chosen variant, a sold-out colour and an
     unpriced product disable the button, adding, changing past the stock,
     removing, two adds at the same moment, at 1440px and 390px.
  4. Holds a write lock on the database while adding, to check the failure
     message and the way back.
  5. Prints one PASS/FAIL line per check and exits non-zero on any FAIL.
"""
import sqlite3
import sys
import time
from decimal import Decimal
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, otp_for, scalar  # noqa: E402

h = Harness("verify-cart", 5288)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
EMAIL = f"cart{STAMP}@example.com"
PASSWORD = "Password1"

OPTIONS = """
with opt as (select vo.VariantId, ot.Code, ov.Value from VariantOptions vo
             join OptionTypes ot on ot.Id = vo.OptionTypeId join OptionValues ov on ov.Id = vo.OptionValueId)
"""


def rows(db_path, sql, *args):
    with sqlite3.connect(db_path) as db:
        return db.execute(sql, args).fetchall()


def variants(db_path, config, color):
    return [r[0] for r in rows(db_path, OPTIONS + """
        select v.Id from ProductVariants v
        join opt c on c.VariantId = v.Id and c.Code = 'config' and c.Value = ?
        join opt col on col.VariantId = v.Id and col.Code = 'color' and col.Value = ?
        where v.Status = 1 order by v.Id""", config, color)]


def config_url(slug, config):
    """The product page's own link to a configuration, so the slug rule is the app's."""
    r = step(r"""
  await page.goto(APP + '/Products/__SLUG__');
  const links = await page.locator('a[href^="/Products/__SLUG__/"]').evaluateAll(as => as.map(a => [a.innerText.trim(), a.getAttribute('href')]));
  return JSON.stringify(links);
""".replace("__SLUG__", slug))
    return next(href.split("?")[0] for text, href in r if config in text)


def main():
    db = TMP / "app.db"
    log = TMP / "app.log"
    h.open_browser()
    migrate(db)

    slug, config, price = rows(db, OPTIONS + """
        select p.Slug, c.Value, min(v.Price) from ProductVariants v join Products p on p.Id = v.ProductId
        join opt c on c.VariantId = v.Id and c.Code = 'config'
        where v.Status = 1 and p.Status = 1 and v.Price is not null
        group by p.Slug, c.Value
        having count(distinct v.Price) = 1 and count(*) >= 4 order by p.Slug limit 1""")[0]
    colors = [r[0] for r in rows(db, OPTIONS + """
        select distinct col.Value from ProductVariants v
        join opt c on c.VariantId = v.Id and c.Code = 'config' and c.Value = ?
        join opt col on col.VariantId = v.Id and col.Code = 'color' where v.Status = 1""", config)]
    stocked, sold_out = colors[0], colors[1]
    with sqlite3.connect(db) as conn:
        conn.execute(f"update ProductVariants set StockQty = 3 where Id in ({','.join(map(str, variants(db, config, stocked)))})")
        conn.execute(f"update ProductVariants set StockQty = 0 where Id in ({','.join(map(str, variants(db, config, sold_out)))})")
    unpriced_slug, unpriced_config = rows(db, OPTIONS + """
        select p.Slug, c.Value from ProductVariants v join Products p on p.Id = v.ProductId
        join opt c on c.VariantId = v.Id and c.Code = 'config'
        where v.Status = 1 and p.Status = 1 and v.Price is null limit 1""")[0]
    # EF Core stores decimals as text in SQLite.
    price = int(Decimal(price))
    price_text = f"{price:,}".replace(",", ".") + " VNĐ"
    triple_text = f"{price * 3:,}".replace(",", ".") + " VNĐ"

    proc = h.start_app("Development", db, log)
    try:
        url = config_url(slug, config)
        unpriced_url = config_url(unpriced_slug, unpriced_config)
        print(f"      using {url} ({stocked} has 3, {sold_out} sold out), unpriced {unpriced_url}")

        # 0. A customer, registered through the real form, then signed out.
        step(r"""
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: 'Cart Check', Phone: '', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__E__", EMAIL).replace("__P__", PASSWORD))
        r = step(r"""
  await fill(page, { Code: '__CODE__' }); await submit(page);
  const out = { url: page.url().replace(APP, '') };
  await signOut(page);
  return JSON.stringify(out);
""".replace("__CODE__", otp_for(log, EMAIL) or ""))
        check("a customer registered through the real form", r["url"] == "/Account", r["url"])

        # 1. Visitor: a sign-in link that follows the chip and comes back to it.
        r = step(r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(APP + '__URL__');
  const out = {};
  out.form = await page.locator('form.pdp-buy').count();
  out.navCount = await page.locator('[data-cart-count]').count();
  await page.click('[data-choice-color="__C__"]');
  out.href = await page.getAttribute('[data-choice-signin]', 'href');
  await page.click('[data-choice-signin]');
  await page.waitForLoadState('load');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
  out.back = decodeURIComponent(page.url().replace(APP, ''));
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__URL__", url).replace("__C__", stocked).replace("__E__", EMAIL).replace("__P__", PASSWORD))
        check("a visitor gets no form and no count, only a sign-in link", r["form"] == 0 and r["navCount"] == 0, str(r))
        check("the sign-in link follows the chosen colour", "color%3D" in r["href"] and stocked.replace(" ", "%20") in r["href"], r["href"])
        check("signing in comes back to that colour", r["back"].startswith(url) and f"color={stocked}" in r["back"].replace("+", " "), r["back"])

        # 2. The form posts the chosen variant; a sold-out colour disables it.
        stocked_ids = variants(db, config, stocked)
        sold_ids = variants(db, config, sold_out)
        r = step(r"""
  const out = {};
  out.first = await page.inputValue('[data-choice-variant]');
  out.firstDisabled = await page.isDisabled('[data-choice-buy]');
  await page.click('[data-choice-color="__S__"]');
  out.sold = await page.inputValue('[data-choice-variant]');
  out.soldDisabled = await page.isDisabled('[data-choice-buy]');
  out.soldReturn = await page.inputValue('[data-choice-return]');
  await page.click('[data-choice-color="__C__"]');
  out.againDisabled = await page.isDisabled('[data-choice-buy]');
  out.again = await page.inputValue('[data-choice-variant]');
  await page.goto(APP + '__UNPRICED__');
  out.unpricedDisabled = await page.isDisabled('[data-choice-buy]');
  return JSON.stringify(out);
""".replace("__S__", sold_out).replace("__C__", stocked).replace("__UNPRICED__", unpriced_url))
        check("the form holds the chosen colour's variant", int(r["first"]) in stocked_ids and not r["firstDisabled"], str(r))
        check("a sold-out colour switches the variant and disables the button",
              int(r["sold"]) in sold_ids and r["soldDisabled"] and "color=" in r["soldReturn"], str(r))
        check("back on a colour in stock the button works again", int(r["again"]) in stocked_ids and not r["againDisabled"], str(r))
        check("a product with no price cannot be added", r["unpricedDisabled"])

        # 3. Add, add again, change past the stock, change, at desktop width.
        r = step(r"""
  const out = {};
  await page.goto(APP + '__URL__?color=' + encodeURIComponent('__C__'));
  out.variant = await page.inputValue('[data-choice-variant]');
  await page.click('[data-choice-buy]'); await page.waitForLoadState('load');
  out.url = page.url().replace(APP, '');
  out.status = await text(page, '.cart-status');
  out.name = await text(page, '.cart-line-name');
  out.options = await text(page, '.cart-line-options');
  out.count1 = await page.locator('[data-cart-count]').innerText();
  out.subtotal1 = await page.locator('[data-cart-subtotal]').innerText();
  await page.goto(APP + '__URL__?color=' + encodeURIComponent('__C__'));
  await page.click('[data-choice-buy]'); await page.waitForLoadState('load');
  out.qty2 = await page.inputValue('.cart-qty-form input[name=Quantity]');
  out.count2 = await page.locator('[data-cart-count]').innerText();
  await page.fill('.cart-qty-form input[name=Quantity]', '9');
  await page.evaluate(() => document.querySelector('.cart-qty-form input[name=Quantity]').removeAttribute('max'));
  await page.click('.cart-qty-form button'); await page.waitForLoadState('load');
  out.tooMany = await text(page, '.cart-error');
  out.qtyAfterTooMany = await page.inputValue('.cart-qty-form input[name=Quantity]');
  await page.fill('.cart-qty-form input[name=Quantity]', '3');
  await page.click('.cart-qty-form button'); await page.waitForLoadState('load');
  out.subtotal3 = await page.locator('[data-cart-subtotal]').innerText();
  await page.screenshot({ path: 'cart-1440.png', fullPage: true });
  return JSON.stringify(out);
""".replace("__URL__", url).replace("__C__", stocked))
        check("adding goes to the cart and says so", r["url"] == "/Cart" and r["status"] == ["Added to your cart."], str(r))
        check("the line names the configuration and colour", r["name"] == [config] and stocked in r["options"][0], str(r))
        check("count and subtotal after one", r["count1"] == "1" and r["subtotal1"] == price_text, f"{r['count1']} {r['subtotal1']} vs {price_text}")
        check("adding again raises the same line", r["qty2"] == "2" and r["count2"] == "2", str(r))
        check("past the stock is refused and keeps the quantity", r["tooMany"] == ["Only 3 in stock."] and r["qtyAfterTooMany"] == "2", str(r))
        check("changing to 3 updates the subtotal", r["subtotal3"] == triple_text, f"{r['subtotal3']} vs {triple_text}")
        line_rows = rows(db, "select VariantId, Quantity from CartItems")
        check("the database holds one line of 3 for that variant", line_rows == [(int(r["variant"]), 3)], str(line_rows))

        # 4. Two adds at the same moment (a double click, two tabs): one line, both counted.
        other = stocked_ids[1] if len(stocked_ids) > 1 else None
        r = step(r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.goto(APP + '__URL__?color=' + encodeURIComponent('__C__'));
  const statuses = await page.evaluate(async id => {
    const f = document.querySelector('form.pdp-buy');
    const send = () => { const d = new FormData(f); d.set('VariantId', id); return fetch(f.action, { method: 'POST', body: d, redirect: 'manual' }).then(x => x.type); };
    return Promise.all([send(), send()]);
  }, '__OTHER__');
  return JSON.stringify({ statuses, consoleErrors });
""".replace("__URL__", url).replace("__C__", stocked).replace("__OTHER__", str(other)))
        race_rows = rows(db, "select count(*), sum(Quantity) from CartItems where VariantId = ?", other)
        check("two adds at once make one line holding both", race_rows == [(1, 2)], f"{race_rows} {r}")

        # 5. Phone width: cart and product page fit; then remove everything.
        r = step(r"""
  const out = {};
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(APP + '/Cart');
  out.cartScroll = await page.evaluate(() => document.documentElement.scrollWidth);
  await page.screenshot({ path: 'cart-390.png', fullPage: true });
  await page.goto(APP + '__URL__');
  out.pdpScroll = await page.evaluate(() => document.documentElement.scrollWidth);
  out.buttonVisible = await page.isVisible('[data-choice-buy]');
  await page.screenshot({ path: 'product-390.png', fullPage: true });
  await page.goto(APP + '/Cart');
  while (await page.locator('.cart-remove').count()) {
    await page.click('.cart-remove'); await page.waitForLoadState('load');
  }
  out.empty = await text(page, '.account-lead');
  out.status = await text(page, '.cart-status');
  out.count = await page.locator('[data-cart-count]').innerText();
  out.browse = await page.getAttribute('main a.btn-store-primary', 'href');
  await page.setViewportSize({ width: 1440, height: 900 });
  return JSON.stringify(out);
""".replace("__URL__", url))
        check("no sideways scroll at 390px (cart and product)", r["cartScroll"] <= 390 and r["pdpScroll"] <= 390, str(r))
        check("the add button is on screen at 390px", r["buttonVisible"])
        check("removing every line leaves the empty state with a way on",
              r["empty"] == ["Your cart is empty."] and r["status"] == ["Removed from your cart."] and r["count"] == "0" and r["browse"] == "/Products", str(r))
        check("no line left in the database", scalar(db, "select count(*) from CartItems") == 0)
    finally:
        h.stop_app(proc)

    # 6. The database refuses the write: a message and the page, not an error.
    lock_log = TMP / "app-lock.log"
    proc = h.start_app("Development", db, lock_log, {"ConnectionStrings__Default": f"Data Source={db};Default Timeout=2"})
    try:
        step(r"""
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
  await page.goto(APP + '__URL__?color=' + encodeURIComponent('__C__'));
  return JSON.stringify({});
""".replace("__E__", EMAIL).replace("__P__", PASSWORD).replace("__URL__", url).replace("__C__", stocked))
        holder = sqlite3.connect(db, isolation_level=None)
        holder.execute("BEGIN IMMEDIATE")
        try:
            r = step(r"""
  await page.click('[data-choice-buy]'); await page.waitForLoadState('load');
  const out = { url: decodeURIComponent(page.url().replace(APP, '')), error: await text(page, '.cart-error'),
                button: await page.isVisible('[data-choice-buy]') };
  await page.screenshot({ path: 'cart-db-failure.png', fullPage: true });
  return JSON.stringify(out);
""")
        finally:
            holder.execute("ROLLBACK")
            holder.close()
        check("a locked database gives the message on the same product page",
              r["url"].startswith(url) and r["error"] == ["We could not update your cart. Please try again."] and r["button"], str(r))
        check("the failure is in the server log", "Cart add failed" in lock_log.read_text())
        r = step(r"""
  await page.click('[data-choice-buy]'); await page.waitForLoadState('load');
  return JSON.stringify({ url: page.url().replace(APP, ''), status: await text(page, '.cart-status') });
""")
        check("once the lock is gone the same button works", r["url"] == "/Cart" and r["status"] == ["Added to your cart."], str(r))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
