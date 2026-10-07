"""Live check of checkout and vouchers (use cases 15-16) in a real browser,
against the real app and a throwaway database built by the real migrations
(the demo vouchers WELCOME10, GIAM500K and AIRPODS15 included).

Usage (from the repo root, playwright-cli installed, nothing on port 5289):
  python3 setup/verify-checkout/verify.py

What it does:
  1. Registers a customer through the real form and saves a default address.
  2. Puts a watch and AirPods Pro 3 in the cart from their product pages,
     checks out: address filled in, vouchers applied and refused, Enter in
     the voucher box never places an order, the order placed and shown.
  3. Checks the database: frozen prices, stock taken, voucher used, cash on
     delivery recorded, cart empty.
  4. Two submits at the same moment, a price changed while the page was
     open, a write lock held while placing, an empty cart, 1440px and 390px.
  5. Prints one PASS/FAIL line per check and exits non-zero on any FAIL.
"""
import sqlite3
import sys
import time
from decimal import Decimal
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, otp_for, rows, scalar  # noqa: E402

h = Harness("verify-checkout", 5289)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
EMAIL = f"checkout{STAMP}@example.com"
PASSWORD = "Password1"


def vnd(amount):
    return f"{int(amount):,}".replace(",", ".") + " VNĐ"


ADD = r"""
  await page.goto(APP + '__URL__');
  await page.click('[data-choice-buy]'); await page.waitForLoadState('load');
"""


def main():
    db = TMP / "app.db"
    log = TMP / "app.log"
    h.open_browser()
    migrate(db)

    # A watch configuration and AirPods Pro 3 (product 32, in AIRPODS15), both priced and in stock.
    watch_slug, watch_config, watch_price = rows(db, """
        select p.Slug, ov.Value, min(v.Price) from ProductVariants v join Products p on p.Id = v.ProductId
        join VariantOptions vo on vo.VariantId = v.Id join OptionTypes ot on ot.Id = vo.OptionTypeId and ot.Code = 'config'
        join OptionValues ov on ov.Id = vo.OptionValueId
        where p.Slug = 'apple-watch-11' and v.Status = 1 and v.Price is not null and v.StockQty > 2
        group by ov.Value having count(distinct v.Price) = 1 order by ov.Value limit 1""")[0]
    pods_slug = scalar(db, "select Slug from Products where Id = 32")
    with sqlite3.connect(db) as conn:
        conn.execute("update ProductVariants set StockQty = 10 where ProductId = 32 and Status = 1 and Price is not null")
        # Enough stock for the five orders below.
        conn.execute("update ProductVariants set StockQty = 10 where Id in (select VariantId from VariantOptions vo join OptionValues ov on ov.Id = vo.OptionValueId where ov.Value = ?)", (watch_config,))
    watch_price, = [int(Decimal(watch_price))]

    proc = h.start_app("Development", db, log)
    try:
        watch_url = h.config_url(watch_slug, watch_config)
        # Sold as a single model: no "config" option, so the configuration carries the product's name.
        pods_config = rows(db, """select ov.Value from ProductVariants v join VariantOptions vo on vo.VariantId = v.Id
            join OptionTypes ot on ot.Id = vo.OptionTypeId and ot.Code = 'config' join OptionValues ov on ov.Id = vo.OptionValueId
            where v.ProductId = 32 and v.Status = 1 and v.Price is not null limit 1""")
        pods_config = pods_config[0][0] if pods_config else scalar(db, "select Name from Products where Id = 32")
        pods_url = h.config_url(pods_slug, pods_config)
        print(f"      using {watch_url} and {pods_url}")

        # 0. Customer and a saved default address.
        step(r"""
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: 'Checkout Check', Phone: '0933333333', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__E__", EMAIL).replace("__P__", PASSWORD))
        r = step(r"""
  await fill(page, { Code: '__CODE__' }); await submit(page);
  await page.goto(APP + '/Account/Addresses/Create');
  await fill(page, { FullName: 'Saved Receiver', Phone: '0944444444', AddressLine: '12 Nguyen Hue', Ward: 'Ben Nghe', District: 'District 1', City: 'Ho Chi Minh City' });
  await page.check('#IsDefault');
  await submit(page);
  return JSON.stringify({ url: page.url().replace(APP, '') });
""".replace("__CODE__", otp_for(log, EMAIL) or ""))
        check("customer registered and a default address saved", r["url"] == "/Account/Addresses", r["url"])

        # 1. Empty cart: checkout sends back to the cart.
        r = step(r"""
  await page.goto(APP + '/Checkout');
  return JSON.stringify({ url: page.url().replace(APP, ''), error: await text(page, '.cart-error') });
""")
        check("an empty cart cannot check out", r["url"] == "/Cart" and r["error"] == ["Your cart is empty."], str(r))

        # 2. Cart, then checkout with the saved address.
        r = step(ADD.replace("__URL__", watch_url) + ADD.replace("__URL__", pods_url) + r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.click('a[href="/Checkout"]'); await page.waitForLoadState('load');
  const out = { url: page.url().replace(APP, '') };
  for (const f of ['FullName', 'Phone', 'AddressLine', 'City']) out[f] = await page.inputValue('#' + f);
  out.total = await page.locator('[data-checkout-total]').innerText();
  out.shipping = await page.locator('.checkout-totals dd').nth(1).innerText();
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""")
        pods_price = int(Decimal(scalar(db, "select min(Price) from ProductVariants where ProductId = 32 and Status = 1 and Price is not null")))
        subtotal = watch_price + pods_price
        check("the cart's Checkout button opens checkout with the default address",
              r["url"] == "/Checkout" and (r["FullName"], r["Phone"], r["AddressLine"], r["City"]) == ("Saved Receiver", "0944444444", "12 Nguyen Hue", "Ho Chi Minh City"), str(r))
        check("checkout shows the subtotal as the total, shipping free", r["total"] == vnd(subtotal) and r["shipping"] == "Free", f"{r['total']} vs {vnd(subtotal)}")

        # 3. Vouchers: product-scoped, minimum order, unknown; Enter never places.
        r = step(r"""
  const out = {};
  const apply = async code => {
    await page.fill('#VoucherCode', code);
    await page.click('button[value=apply]'); await page.waitForLoadState('load');
    return { msg: await page.locator('[data-voucher-message]').innerText(), total: await page.locator('[data-checkout-total]').innerText() };
  };
  out.pods = await apply('airpods15');
  out.nope = await apply('NOPE');
  await page.fill('#AddressLine', '99 Typed Street');
  out.big = await apply('giam500k');
  out.kept = await page.inputValue('#AddressLine');
  await page.focus('#VoucherCode');
  await Promise.all([page.waitForNavigation({ timeout: 10000 }).catch(() => null), page.keyboard.press('Enter')]);
  await page.waitForLoadState('load');
  out.afterEnter = page.url().replace(APP, '');
  await page.screenshot({ path: 'checkout-1440.png', fullPage: true });
  return JSON.stringify(out);
""")
        pods_off = int((Decimal(pods_price) * Decimal("0.15")).quantize(Decimal("1"), rounding="ROUND_HALF_UP"))
        check("AIRPODS15 takes 15% off the AirPods line only",
              r["pods"]["msg"] == "Voucher AIRPODS15 applied." and r["pods"]["total"] == vnd(subtotal - pods_off), f"{r['pods']} vs {vnd(subtotal - pods_off)}")
        check("an unknown code says so and the total goes back", r["nope"]["msg"] == "There is no voucher with that code." and r["nope"]["total"] == vnd(subtotal), str(r["nope"]))
        check("GIAM500K takes 500.000 off an order over 10 million", r["big"]["total"] == vnd(subtotal - 500_000), f"{r['big']} vs {vnd(subtotal - 500_000)}")
        check("what was typed survives applying a voucher", r["kept"] == "99 Typed Street", r["kept"])
        check("Enter in the voucher box places no order", r["afterEnter"] == "/Checkout" and scalar(db, "select count(*) from Orders") == 0, r["afterEnter"])

        # 4. Place the order.
        stock_before = rows(db, "select VariantId, (select StockQty from ProductVariants where Id = VariantId) from CartItems order by Id")
        r = step(r"""
  await page.fill('#Note', 'Ring twice');
  await page.click('button[value=place]'); await page.waitForLoadState('load');
  const out = { url: page.url().replace(APP, ''), status: await text(page, '.cart-status'), title: await page.locator('h1').innerText(),
                count: await page.locator('[data-cart-count]').innerText(), body: await page.locator('main').innerText() };
  await page.screenshot({ path: 'order-1440.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload(); await page.waitForLoadState('load');
  out.scroll = await page.evaluate(() => document.documentElement.scrollWidth);
  out.height = await page.evaluate(() => document.documentElement.scrollHeight);
  await page.screenshot({ path: 'order-390.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  return JSON.stringify(out);
""")
        order_id = scalar(db, "select max(Id) from Orders")
        check("placing goes to the order page and says thank you",
              r["url"] == f"/Orders/{order_id}" and r["status"] == ["Thank you. Your order was placed."] and r["title"] == f"Order #{order_id}", str({k: r[k] for k in ("url", "status", "title")}))
        check("the order page shows the note, cash on delivery and the total",
              "Ring twice" in r["body"] and "Cash on delivery" in r["body"] and vnd(subtotal - 500_000) in r["body"] and "99 Typed Street" in r["body"], r["body"][:300])
        check("the nav count is back to 0", r["count"] == "0", r["count"])
        check("the order page fits 390px", r["scroll"] <= 390, str(r["scroll"]))
        order = rows(db, "select Subtotal, DiscountAmount, ShippingFee, TotalAmount, VoucherCode, Status, PaymentStatus from Orders where Id = ?", order_id)[0]
        check("the order row holds the totals, the voucher and pending/unpaid",
              (int(Decimal(order[0])), int(Decimal(order[1])), int(Decimal(order[2])), int(Decimal(order[3])), order[4], order[5], order[6])
              == (subtotal, 500_000, 0, subtotal - 500_000, "GIAM500K", 0, 0), str(order))
        stock_after = {v: s for v, s in rows(db, "select Id, StockQty from ProductVariants where Id in (select VariantId from OrderItems where OrderId = ?)", order_id)}
        check("stock went down by one for each line", all(stock_after[v] == s - 1 for v, s in stock_before), f"{stock_before} -> {stock_after}")
        check("the voucher use is counted and COD is recorded",
              scalar(db, "select UsedCount from Vouchers where Code = 'GIAM500K'") == 1
              and rows(db, "select Method, Status from Payments where OrderId = ?", order_id) == [(0, 0)])
        check("the cart is empty in the database", scalar(db, "select count(*) from CartItems") == 0)

        # 5. Two submits at the same moment: one order.
        r = step(ADD.replace("__URL__", watch_url) + r"""
  await page.goto(APP + '/Checkout');
  const results = await page.evaluate(async () => {
    const f = document.querySelector('form.checkout');
    const send = () => { const d = new FormData(f); d.set('Intent', 'place'); return fetch(f.action, { method: 'POST', body: d, redirect: 'manual' }).then(x => x.type); };
    return Promise.all([send(), send()]);
  });
  return JSON.stringify({ results });
""")
        check("two submits at once make one order", scalar(db, "select count(*) from Orders") == 2 and scalar(db, "select count(*) from CartItems") == 0, str(r))

        # 6. A price changed while the page was open: shown again, then placed.
        step(ADD.replace("__URL__", watch_url) + "await page.goto(APP + '/Checkout'); return JSON.stringify({});")
        with sqlite3.connect(db) as conn:
            conn.execute("update ProductVariants set Price = Price + 100000 where Id = (select VariantId from CartItems limit 1)")
        r = step(r"""
  await page.click('button[value=place]'); await page.waitForLoadState('load');
  const out = { url: page.url().replace(APP, ''), error: await text(page, '.account-error'), total: await page.locator('[data-checkout-total]').innerText() };
  await page.click('button[value=place]'); await page.waitForLoadState('load');
  out.after = page.url().replace(APP, '');
  return JSON.stringify(out);
""")
        check("a changed price is shown again with the new total",
              r["url"] == "/Checkout" and r["error"] == ["The total changed since you opened this page. Check it and place the order again."]
              and r["total"] == vnd(watch_price + 100_000), str(r))
        check("placing again after seeing it goes through", r["after"].startswith("/Orders/"), r["after"])

        # 7. Checkout at phone width.
        r = step(ADD.replace("__URL__", watch_url) + r"""
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(APP + '/Checkout');
  const out = { scroll: await page.evaluate(() => document.documentElement.scrollWidth), placeVisible: await page.isVisible('button[value=place]') };
  await page.screenshot({ path: 'checkout-390.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  return JSON.stringify(out);
""")
        check("checkout fits 390px and the place button is there", r["scroll"] <= 390 and r["placeVisible"], str(r))
    finally:
        h.stop_app(proc)

    # 8. The database refuses the write while placing: a message, nothing half-done.
    lock_log = TMP / "app-lock.log"
    orders_before = scalar(db, "select count(*) from Orders")
    proc = h.start_app("Development", db, lock_log, {"ConnectionStrings__Default": f"Data Source={db};Default Timeout=2"})
    try:
        step(r"""
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
  await page.goto(APP + '/Checkout');
  return JSON.stringify({});
""".replace("__E__", EMAIL).replace("__P__", PASSWORD))
        holder = sqlite3.connect(db, isolation_level=None)
        holder.execute("BEGIN IMMEDIATE")
        try:
            r = step(r"""
  await page.click('button[value=place]'); await page.waitForLoadState('load');
  const out = { url: page.url().replace(APP, ''), error: await text(page, '.cart-error') };
  await page.screenshot({ path: 'checkout-db-failure.png', fullPage: true });
  return JSON.stringify(out);
""")
        finally:
            holder.execute("ROLLBACK")
            holder.close()
        check("a locked database says nothing was placed",
              r["url"] == "/Checkout" and r["error"] == ["We could not place your order, and nothing was charged. Please try again."], str(r))
        check("no order and the cart kept", scalar(db, "select count(*) from Orders") == orders_before and scalar(db, "select count(*) from CartItems") == 1)
        check("the failure is in the server log", "Placing an order failed" in lock_log.read_text())
        r = step(r"""
  await page.click('button[value=place]'); await page.waitForLoadState('load');
  return JSON.stringify({ url: page.url().replace(APP, '') });
""")
        check("once the lock is gone the same button places it", r["url"].startswith("/Orders/"), r["url"])
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
