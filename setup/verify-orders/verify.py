"""Live check of orders after checkout (use cases 18-19, 22-24, 37) in a real
browser, against the real app and a throwaway database built by the real
migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5291):
  python3 setup/verify-orders/verify.py

What it does:
  1. Registers a customer and a second account that is made an Employee in
     the throwaway database (there is no user admin page yet).
  2. Customer: places orders, sees "My orders", cancels a pending one (stock
     comes back), gets the order email (read from the app log).
  3. Employee: the Staff link, the order list and filters, confirm, ship
     (with and without a tracking number), complete; an unpaid online order
     waits for payment; a stale page; the dashboard stays closed.
  4. Anyone: /Track with the order number and phone, and with a wrong phone.
  5. 1440px and 390px; prints PASS/FAIL per check, exits non-zero on a FAIL.
"""
import sqlite3
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, otp_for, rows, scalar  # noqa: E402

h = Harness("verify-orders", 5291)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
CUSTOMER = f"orders{STAMP}@example.com"
STAFF = f"staff{STAMP}@example.com"
PASSWORD = "Password1"
PHONE = "0977 123 456"

PLACE = r"""
  await page.goto(APP + '__URL__');
  await page.click('[data-choice-buy]'); await page.waitForLoadState('load');
  await page.goto(APP + '/Checkout');
  await page.fill('#Phone', '__PHONE__');
  await page.fill('#AddressLine', '9 Order Street');
  await page.check('input[name=PaymentMethod][value=__METHOD__]');
  await page.click('button[value=place]'); await page.waitForLoadState('load');
"""


def register(log, email, name):
    step(r"""
  await signOut(page);
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: '__N__', Phone: '', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__E__", email).replace("__N__", name).replace("__P__", PASSWORD))
    return step(r"""
  await fill(page, { Code: '__CODE__' }); await submit(page);
  return JSON.stringify({ url: page.url().replace(APP, '') });
""".replace("__CODE__", otp_for(log, email) or ""))


def sign_in(email):
    return r"""
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
""".replace("__E__", email).replace("__P__", PASSWORD)


def main():
    db = TMP / "app.db"
    log = TMP / "app.log"
    h.open_browser()
    migrate(db)
    slug, config = rows(db, """
        select p.Slug, ov.Value from ProductVariants v join Products p on p.Id = v.ProductId
        join VariantOptions vo on vo.VariantId = v.Id join OptionTypes ot on ot.Id = vo.OptionTypeId and ot.Code = 'config'
        join OptionValues ov on ov.Id = vo.OptionValueId
        where p.Slug = 'apple-watch-11' and v.Status = 1 and v.Price is not null
        group by ov.Value having count(distinct v.Price) = 1 order by ov.Value limit 1""")[0]
    with sqlite3.connect(db) as conn:
        conn.execute("update ProductVariants set StockQty = 20 where ProductId = (select Id from Products where Slug = ?)", (slug,))

    proc = h.start_app("Development", db, log)
    try:
        url = h.config_url(slug, config)
        check("customer registered", register(log, CUSTOMER, "Order Customer")["url"] == "/Account")
        check("second account registered", register(log, STAFF, "Store Staff")["url"] == "/Account")
        with sqlite3.connect(db) as conn:
            conn.execute("update Users set Role = 1 where Email = ?", (STAFF,))

        # 1. Customer: two cash orders, one online order left unpaid.
        r = step(sign_in(CUSTOMER) + PLACE.replace("__URL__", url).replace("__PHONE__", PHONE).replace("__METHOD__", "Cod") + r"""
  const out = { first: page.url().replace(APP, '') };
""" + PLACE.replace("__URL__", url).replace("__PHONE__", PHONE).replace("__METHOD__", "Cod") + r"""
  out.second = page.url().replace(APP, '');
""" + PLACE.replace("__URL__", url).replace("__PHONE__", PHONE).replace("__METHOD__", "VnPay") + r"""
  out.online = page.url().replace(APP, '');
  await page.goto(APP + '/Account');
  await page.click('text=My orders'); await page.waitForLoadState('load');
  out.list = await page.locator('.order-list-link').evaluateAll(as => as.map(a => a.getAttribute('href')));
  return JSON.stringify(out);
""")
        first, second = int(r["first"].split("/")[-1]), int(r["second"].split("/")[-1])
        online = scalar(db, "select max(Id) from Orders")
        check("cash orders land on their order pages", r["first"] == f"/Orders/{first}" and r["second"] == f"/Orders/{second}", str(r))
        check("My orders lists all three, newest first", r["list"] == [f"/Orders/{online}", f"/Orders/{second}", f"/Orders/{first}"], str(r["list"]))
        check("the order email is sent (logged in Development)", f"Order #{first} received" in log.read_text())

        # 2. Customer cancels the second order; the stock comes back.
        stock_before = scalar(db, "select StockQty from ProductVariants where Id = (select VariantId from OrderItems where OrderId = ?)", second)
        r = step(r"""
  await page.goto(APP + '/Orders/__ID__');
  await page.click('text=Cancel this order'); await page.waitForLoadState('load');
  return JSON.stringify({ status: await text(page, '.cart-status'), button: await page.locator('text=Cancel this order').count() });
""".replace("__ID__", str(second)))
        stock_after = scalar(db, "select StockQty from ProductVariants where Id = (select VariantId from OrderItems where OrderId = ?)", second)
        check("a pending order is cancelled by its customer", r["status"] == ["Your order was cancelled."] and r["button"] == 0, str(r))
        check("its stock comes back", stock_after == stock_before + 1, f"{stock_before} -> {stock_after}")

        # 3. Staff: link, list, waiting online order, the first order end to end.
        r = step(sign_in(STAFF) + r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  const out = { link: await page.locator('a.site-nav-account-link[href="/Admin/Orders"]').innerText() };
  await page.click('a[href="/Admin/Orders"]'); await page.waitForLoadState('load');
  out.list = await page.locator('.order-list-link').count();
  await page.goto(APP + '/Admin/Orders?status=Cancelled');
  out.cancelled = await page.locator('.order-list-link').evaluateAll(as => as.map(a => a.getAttribute('href')));
  await page.goto(APP + '/Admin/Orders/__ONLINE__');
  out.waiting = await page.locator('[data-order-actions]').innerText();
  await page.goto(APP + '/Admin/Orders/__FIRST__');
  await page.click('button[value=Confirm]'); await page.waitForLoadState('load');
  out.confirmed = await text(page, '.cart-status');
  await page.click('button[value=Ship]'); await page.waitForLoadState('load');
  out.noTracking = await text(page, '.cart-error');
  await page.fill('#Carrier', 'GHN'); await page.fill('#TrackingNo', 'GHN555');
  await page.click('button[value=Ship]'); await page.waitForLoadState('load');
  out.shipped = await text(page, '.cart-status');
  await page.screenshot({ path: 'staff-order-1440.png', fullPage: true });
  await page.click('button[value=Complete]'); await page.waitForLoadState('load');
  out.completed = await text(page, '.cart-status');
  out.buttonsLeft = await page.locator('[data-order-actions] form').count();
  const dash = await page.goto(APP + '/Admin');
  out.dashboard = page.url().replace(APP, '').split('?')[0];
  out.denied = await page.locator('main').innerText();
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__ONLINE__", str(online)).replace("__FIRST__", str(first)))
        check("an employee gets the Staff link to the orders", r["link"] == "Staff" and r["list"] == 3, str({k: r[k] for k in ("link", "list")}))
        check("the status filter shows the cancelled order only", r["cancelled"] == [f"/Admin/Orders/{second}"], str(r["cancelled"]))
        check("an unpaid online order waits for payment, no confirm button",
              "Waiting for payment" in r["waiting"] and "Confirm order" not in r["waiting"], r["waiting"])
        check("confirm, ship without tracking refused, ship, complete",
              r["confirmed"] == ["Order confirmed."] and r["noTracking"] == ["Enter the carrier and the tracking number."]
              and r["shipped"] == ["Order shipped. The customer was emailed the tracking number."]
              and r["completed"] == ["Order completed."] and r["buttonsLeft"] == 0, str(r))
        check("the shipped email carries the tracking number", "is on its way" in log.read_text() and "GHN555" in log.read_text())
        state = rows(db, "select Status, PaymentStatus from Orders where Id = ?", first)[0]
        check("the database has it completed and the cash paid", state == (3, 1), str(state))
        check("the dashboard stays closed to employees, with plain wording",
              r["dashboard"] == "/Account/AccessDenied" and "staff accounts only" not in r["denied"] and "Ask an administrator" in r["denied"], r["dashboard"])
        check("no console errors on the staff pages", r["consoleErrors"] == [], str(r["consoleErrors"]))

        # 4. A stale staff page: confirm pressed twice from the same page.
        r = step(r"""
  await page.goto(APP + '/Admin/Orders/__ONLINE__');
  const html = await page.content();
  return JSON.stringify({ hasCancel: html.includes('value="Cancel"') });
""".replace("__ONLINE__", str(online)))
        with sqlite3.connect(db) as conn:
            conn.execute("update Orders set Status = 4 where Id = ?", (online,))
        r = step(r"""
  await page.click('button[value=Cancel]'); await page.waitForLoadState('load');
  return JSON.stringify({ error: await text(page, '.cart-error') });
""")
        check("a stale staff page is told the order changed", r["error"] == ["This order changed meanwhile. Check it and try again."], str(r))

        # 5. Phone width for the staff list, and tracking without signing in.
        r = step(r"""
  const out = {};
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(APP + '/Admin/Orders');
  out.listScroll = await page.evaluate(() => document.documentElement.scrollWidth);
  await page.screenshot({ path: 'staff-orders-390.png', fullPage: true });
  await page.goto(APP + '/Admin/Orders/__FIRST__');
  out.orderScroll = await page.evaluate(() => document.documentElement.scrollWidth);
  // The sign-out button sits in the collapsed menu at 390px.
  await page.setViewportSize({ width: 1440, height: 900 });
  await signOut(page);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(APP + '/Track');
  await page.fill('#OrderId', '__FIRST__'); await page.fill('#Phone', '0977-123-456');
  await page.click('form.account-form button'); await page.waitForLoadState('load');
  out.track = await page.locator('[data-track-result]').innerText();
  out.trackScroll = await page.evaluate(() => document.documentElement.scrollWidth);
  await page.screenshot({ path: 'track-390.png', fullPage: true });
  await page.fill('#Phone', '0900000000');
  await page.click('form.account-form button'); await page.waitForLoadState('load');
  out.wrong = await page.locator('[data-track-result]').innerText();
  await page.setViewportSize({ width: 1440, height: 900 });
  return JSON.stringify(out);
""".replace("__FIRST__", str(first)))
        check("staff pages fit 390px", r["listScroll"] <= 390 and r["orderScroll"] <= 390, str(r))
        check("tracking with the phone shows Delivered and the tracking number, no address or price",
              "Delivered" in r["track"] and "GHN555" in r["track"] and "Order Street" not in r["track"] and "VNĐ" not in r["track"] and r["trackScroll"] <= 390, r["track"])
        check("a wrong phone finds nothing", r["wrong"].strip() == "No order matches that number and phone.", r["wrong"])
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
