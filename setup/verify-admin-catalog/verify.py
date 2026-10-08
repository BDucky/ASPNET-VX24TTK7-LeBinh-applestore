"""Live check of the admin product, voucher and account pages (use cases
25-27, 29-31) in a real browser, against the real app and a throwaway
database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5292):
  python3 setup/verify-admin-catalog/verify.py

What it does:
  1. Seeds an admin through SeedAdmin environment variables and registers a
     customer through the real form.
  2. Admin: searches products, adds one with a photo from the library, adds a
     variant, is refused a variant save after a sale, takes the product off
     sale and back; checks the three photos added on 2026-10-08 show.
  3. Admin creates a voucher; the customer applies it at checkout.
  4. Admin makes the customer an Employee while the customer is signed in
     elsewhere: that session ends, and signing in again shows the Staff link.
  5. 1440px and 390px; PASS/FAIL per check, non-zero exit on any FAIL.
"""
import sqlite3
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, otp_for, rows, scalar  # noqa: E402

h = Harness("verify-admin-catalog", 5292)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
ADMIN = f"admin{STAMP}@example.com"
ADMIN_PASSWORD = f"Admin-{STAMP}"
CUSTOMER = f"buyer{STAMP}@example.com"
PASSWORD = "Password1"
SEED = {"SeedAdmin__Email": ADMIN, "SeedAdmin__Password": ADMIN_PASSWORD, "SeedAdmin__FullName": "Store Admin"}


def sign_in(email, password):
    return r"""
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
""".replace("__E__", email).replace("__P__", password)


def main():
    db = TMP / "app.db"
    log = TMP / "app.log"
    h.open_browser()
    migrate(db)
    proc = h.start_app("Development", db, log, SEED)
    try:
        # 0. A customer through the real form.
        step(r"""
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: 'Live Buyer', Phone: '0966666666', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__E__", CUSTOMER).replace("__P__", PASSWORD))
        r = step(r"""
  await fill(page, { Code: '__CODE__' }); await submit(page);
  return JSON.stringify({ url: page.url().replace(APP, '') });
""".replace("__CODE__", otp_for(log, CUSTOMER) or ""))
        check("customer registered", r["url"] == "/Account", r["url"])

        # 1. The photos added today show on their product pages.
        r = step(r"""
  const out = {};
  for (const slug of ['mac-mini', 'iphone-duo', 'apple-watch-12']) {
    await page.goto(APP + '/Products/' + slug);
    out[slug] = await page.locator('main img').evaluateAll(is => is.map(i => i.getAttribute('src')).filter(s => s.includes('/img/products/')));
  }
  return JSON.stringify(out);
""")
        check("Mac mini, iPhone Duo and Apple Watch 12 now show their photos",
              "/img/products/mac-mini-m4.jpg" in r["mac-mini"] and "/img/products/iphone-duo.jpg" in r["iphone-duo"]
              and "/img/products/apple-watch-series-12-commons.jpg" in r["apple-watch-12"], str(r))

        # 2. Admin: products.
        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.goto(APP + '/Admin/Products?search=watch');
  const out = { found: await page.locator('.order-list-link').count() };
  await press(page, 'text=Add a product');
  out.photos = await page.locator('input[name=ImageUrl]').count();
  await page.fill('#Name', 'Live Check Mac __S__');
  await page.selectOption('#CategoryId', { label: 'Mac' });
  await page.fill('#BasePrice', '15990000');
  await page.check('input[name=ImageUrl][value="/img/products/mac-mini-m4.jpg"]');
  await press(page, 'button:has-text("Save product")');
  out.saved = await text(page, '.cart-status');
  out.edit = page.url().replace(APP, '');
  await page.fill('#Sku', 'LIVE-__S__');
  await page.fill('#Configuration', 'Live Check Mac __S__ 512GB');
  await page.fill('#Color', 'Silver');
  await page.fill('#Price', '15990000');
  await page.fill('#StockQty', '4');
  await press(page, 'button:has-text("Add variant")');
  out.variant = await text(page, '.cart-status');
  await page.screenshot({ path: 'product-edit-1440.png', fullPage: true });
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__S__", STAMP))
        product_id = int(r["edit"].split("/")[-1])
        slug = scalar(db, "select Slug from Products where Id = ?", product_id)
        check("the product list searches", r["found"] >= 5, str(r["found"]))
        check("the form offers the photo library", r["photos"] >= 50, str(r["photos"]))
        check("a product is added and a variant with it", r["saved"] == ["Product saved."] and r["variant"] == ["Variant added."], str(r))
        r = step(r"""
  await page.goto(APP + '/Products/__SLUG__');
  const out = { photo: await page.locator('main img[src="/img/products/mac-mini-m4.jpg"]').count() };
  await press(page, 'a[href*="/Products/__SLUG__/"]');
  out.config = page.url().replace(APP, '');
  out.price = await page.locator('[data-choice-price]').innerText();
  return JSON.stringify(out);
""".replace("__SLUG__", slug))
        check("the new product and its variant are in the shop", r["photo"] >= 1 and r["price"] == "15.990.000 VNĐ", str(r))

        # 3. A sale lands while the admin edits stock; then off sale and back.
        variant_id = scalar(db, "select Id from ProductVariants where SKU = ?", f"LIVE-{STAMP}")
        step(r"""
  await page.goto(APP + '/Admin/Products/__ID__');
  return JSON.stringify({});
""".replace("__ID__", str(product_id)))
        with sqlite3.connect(db) as conn:
            conn.execute("update ProductVariants set StockQty = 3 where Id = ?", (variant_id,))
        r = step(r"""
  const form = page.locator('form[action="/Admin/Products/Variants/__V__"]');
  await form.locator('input[name=StockQty]').fill('40');
  await Promise.all([page.waitForNavigation(), form.locator('button').click()]);
  const out = { stale: await text(page, '.cart-error') };
  await press(page, 'button:has-text("Take off sale")');
  out.off = await text(page, '.cart-status');
  out.shopOff = (await page.request.get(APP + '/Products/__SLUG__')).status();
  await press(page, 'button:has-text("Put back on sale")');
  out.shopOn = (await page.request.get(APP + '/Products/__SLUG__')).status();
  return JSON.stringify(out);
""".replace("__V__", str(variant_id)).replace("__SLUG__", slug))
        check("a stock save after a sale is refused and says the stock now",
              r["stale"] == ["The stock changed to 3 while you were editing (a sale, or another admin). Check it and save again."]
              and scalar(db, "select StockQty from ProductVariants where Id = ?", variant_id) == 3, str(r["stale"]))
        check("off sale hides it from the shop and back on sale shows it", r["shopOff"] == 404 and r["shopOn"] == 200 and r["off"] == ["Taken off sale. Orders that include it are not changed."], str(r))

        # 4. A voucher the admin makes is accepted at checkout.
        r = step(r"""
  await page.goto(APP + '/Admin/Vouchers/New');
  await page.fill('#Code', 'live__S__');
  await page.selectOption('#Type', 'Percent');
  await page.fill('#Value', '5');
  await press(page, 'button:has-text("Save voucher")');
  const out = { saved: await text(page, '.cart-status') };
  await page.screenshot({ path: 'voucher-edit-1440.png', fullPage: true });
  return JSON.stringify(out);
""".replace("__S__", STAMP))
        check("a voucher is created and stored upper case", r["saved"] == ["Voucher saved."] and scalar(db, "select count(*) from Vouchers where Code = ?", f"LIVE{STAMP}") == 1, str(r))
        r = step(sign_in(CUSTOMER, PASSWORD) + r"""
  await page.goto(APP + '/Products/__SLUG__');
  await press(page, 'a[href*="/Products/__SLUG__/"]');
  await press(page, '[data-choice-buy]');
  await page.goto(APP + '/Checkout');
  await page.fill('#VoucherCode', 'live__S__');
  await press(page, 'button[value=apply]');
  return JSON.stringify({ msg: await page.locator('[data-voucher-message]').innerText(), total: await page.locator('[data-checkout-total]').innerText() });
""".replace("__SLUG__", slug).replace("__S__", STAMP))
        check("the customer gets the admin's voucher at checkout", r["msg"] == f"Voucher LIVE{STAMP} applied." and r["total"] == "15.190.500 VNĐ", str(r))

        # 5. The admin makes the signed-in customer an Employee from another browser.
        customer_id = scalar(db, "select Id from Users where Email = ?", CUSTOMER)
        r = step(r"""
  const other = await page.context().browser().newContext();
  const admin = await other.newPage();
  await admin.goto(APP + '/Account/Login');
  await admin.fill('.account-form #Email', '__A__'); await admin.fill('.account-form #Password', '__AP__');
  await Promise.all([admin.waitForNavigation(), admin.click('.account-form button[type=submit]')]);
  await admin.goto(APP + '/Admin/Users?search=__C__');
  const form = admin.locator('form[action="/Admin/Users/__ID__/Role"]');
  await form.locator('select').selectOption('Employee');
  await Promise.all([admin.waitForNavigation(), form.locator('button').click()]);
  const out = { changed: (await admin.locator('.cart-status').allTextContents()).map(s => s.trim()) };
  await admin.setViewportSize({ width: 390, height: 844 });
  await admin.goto(APP + '/Admin/Products/__PID__');
  out.scroll = await admin.evaluate(() => document.documentElement.scrollWidth);
  await admin.screenshot({ path: 'product-edit-390.png', fullPage: true });
  await other.close();
  await page.goto(APP + '/Account');
  out.customerNow = page.url().replace(APP, '').split('?')[0];
""".replace("__A__", ADMIN).replace("__AP__", ADMIN_PASSWORD).replace("__C__", CUSTOMER).replace("__ID__", str(customer_id)).replace("__PID__", str(product_id))
            + sign_in(CUSTOMER, PASSWORD).replace("await signOut(page);", "") + r"""
  out.staffLink = await page.locator('a.site-nav-account-link[href="/Admin/Orders"]').count();
  return JSON.stringify(out);
""")
        check("the role change is confirmed to the admin", r["changed"] == ["Role changed. Their open sessions were signed out."], str(r["changed"]))
        check("the customer's open session ends at once", r["customerNow"] == "/Account/Login", r["customerNow"])
        check("signing in again shows the Staff link", r["staffLink"] == 1)
        check("the product edit page fits 390px", r["scroll"] <= 390, str(r["scroll"]))
        check("an employee cannot open the product pages",
              step("const resp = await page.goto(APP + '/Admin/Products'); return JSON.stringify({ url: page.url().replace(APP, '') });")["url"].startswith("/Account/AccessDenied"))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
