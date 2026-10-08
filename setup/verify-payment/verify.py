"""Live check of online payment through the simulated gateway (use case 17)
in a real browser, against the real app and a throwaway database built by
the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5290):
  python3 setup/verify-payment/verify.py

What it does:
  1. Registers a customer through the real form.
  2. VNPay: place, pay at /PaymentSimulator, land on the paid order.
  3. MoMo: the payment fails, is cancelled, then is paid through "Pay now".
  4. A tampered gateway link, the same return address opened again.
  5. Starts the app in Production with the simulator on (it must refuse to
     start) and in Development with no gateway (cash on delivery only).
  6. Prints one PASS/FAIL line per check and exits non-zero on any FAIL.
"""
import os
import sqlite3
import subprocess
import sys
import time
from decimal import Decimal
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import WEB, Harness, migrate, otp_for, rows, scalar  # noqa: E402

h = Harness("verify-payment", 5290)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
EMAIL = f"payment{STAMP}@example.com"
PASSWORD = "Password1"

PLACE = r"""
  await page.goto(APP + '__URL__');
  await page.click('[data-choice-buy]'); await page.waitForLoadState('load');
  await page.goto(APP + '/Checkout');
  await page.fill('#Phone', '0955555555');
  await page.fill('#AddressLine', '1 Payment Street');
  await page.check('input[name=PaymentMethod][value=__METHOD__]');
  await page.click('button[value=place]'); await page.waitForLoadState('load');
"""

PRESS = r"""
  await page.click('button[name=result][value="__RESULT__"]'); await page.waitForLoadState('load');
"""


def vnd(amount):
    return f"{int(amount):,}".replace(",", ".") + " VNĐ"


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
        step(r"""
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: 'Payment Check', Phone: '', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__E__", EMAIL).replace("__P__", PASSWORD))
        r = step(r"""
  await fill(page, { Code: '__CODE__' }); await submit(page);
  return JSON.stringify({ url: page.url().replace(APP, '') });
""".replace("__CODE__", otp_for(log, EMAIL) or ""))
        check("customer registered", r["url"] == "/Account", r["url"])

        # 1. VNPay, paid at the first try.
        r = step(PLACE.replace("__URL__", url).replace("__METHOD__", "VnPay") + r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  const out = { simulator: page.url().replace(APP, '').split('?')[0], eyebrow: await page.locator('.hero-eyebrow').innerText(),
                amount: await page.locator('[data-simulator-amount]').innerText() };
  await page.screenshot({ path: 'simulator-1440.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload(); await page.waitForLoadState('load');
  out.scroll = await page.evaluate(() => document.documentElement.scrollWidth);
  await page.screenshot({ path: 'simulator-390.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.reload(); await page.waitForLoadState('load');
""" + PRESS.replace("__RESULT__", "00") + r"""
  out.returned = page.url().replace(APP, '');
  out.status = await text(page, '.cart-status');
  out.body = await page.locator('main').innerText();
  out.payNow = await page.locator('.order-pay').count();
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""")
        first = scalar(db, "select max(Id) from Orders")
        total = int(Decimal(scalar(db, "select TotalAmount from Orders where Id = ?", first)))
        check("VNPay sends the shopper to the simulated gateway with the order total",
              r["simulator"] == "/PaymentSimulator" and r["eyebrow"].upper().startswith("VNPAY") and r["amount"] == vnd(total), str({k: r[k] for k in ("simulator", "eyebrow", "amount")}))
        check("the simulator fits 390px", r["scroll"] <= 390, str(r["scroll"]))
        check("paying lands on the order, paid, with no Pay now",
              r["returned"] == f"/Orders/{first}" and r["status"] == ["Payment received. Thank you."] and "VNPay, paid" in r["body"] and r["payNow"] == 0, str(r["status"]))
        check("no console errors", r["consoleErrors"] == [], str(r["consoleErrors"]))
        check("the database has the order paid and one successful VNPay payment",
              scalar(db, "select PaymentStatus from Orders where Id = ?", first) == 1
              and rows(db, "select Method, Status, TxnId like 'SIM-%', PaidAmount from Payments where OrderId = ?", first)[0][:3] == (1, 1, 1))

        # 2. MoMo: fails, cancelled, then paid through Pay now.
        r = step(PLACE.replace("__URL__", url).replace("__METHOD__", "MoMo") + PRESS.replace("__RESULT__", "51") + r"""
  const out = { failed: await text(page, '.cart-error'), payNow: await page.locator('.order-pay').count() };
  await page.click('.order-pay button'); await page.waitForLoadState('load');
""" + PRESS.replace("__RESULT__", "24") + r"""
  out.cancelled = await text(page, '.cart-error');
  await page.click('.order-pay button'); await page.waitForLoadState('load');
  out.gateway = await page.locator('.hero-eyebrow').innerText();
  const returned = page.waitForRequest(r => r.url().includes('/Payments/Return'));
""" + PRESS.replace("__RESULT__", "00") + r"""
  out.paid = await text(page, '.cart-status');
  out.body = await page.locator('main').innerText();
  out.returnUrl = (await returned).url();
  await page.screenshot({ path: 'order-paid.png', fullPage: true });
  return JSON.stringify(out);
""")
        r_momo = r
        second = scalar(db, "select max(Id) from Orders")
        check("a failed payment says nothing was charged and offers Pay now",
              r["failed"] == ["The payment did not go through, and nothing was charged. You can try again."] and r["payNow"] == 1, str(r))
        check("a cancelled payment says so", r["cancelled"] == ["The payment was cancelled. You can try again."], str(r["cancelled"]))
        check("Pay now opens the MoMo simulator and paying works", r["gateway"].upper().startswith("MOMO") and r["paid"] == ["Payment received. Thank you."] and "MoMo, paid" in r["body"], str(r))
        attempts = rows(db, "select Method, Status from Payments where OrderId = ? order by Id", second)
        check("three attempts recorded: failed, failed (cancelled), success", attempts == [(2, 2), (2, 2), (2, 1)], str(attempts))

        # 3. A tampered link; the same return opened again.
        payment = scalar(db, "select max(Id) from Payments where OrderId = ?", second)
        r = step(r"""
  const out = {};
  const resp = await page.goto(APP + '/PaymentSimulator?paymentId=__P__&orderId=__O__&amount=1&method=MoMo&signature=00');
  out.status = resp.status();
  out.title = await page.locator('h1').innerText();
  out.back = await page.getAttribute('main a.btn-store-primary', 'href');
  return JSON.stringify(out);
""".replace("__P__", str(payment)).replace("__O__", str(second)))
        check("a tampered gateway link is a 400 with a way back",
              r["status"] == 400 and r["title"] == "This payment link is not valid." and r["back"] == "/", str(r))
        return_url = r_momo["returnUrl"]
        before = rows(db, "select Id, Status, TxnId, PaidAt from Payments order by Id")
        r = step(r"""
  await page.goto('__RETURN__');
  return JSON.stringify({ url: page.url().replace(APP, ''), status: await text(page, '.cart-status') });
""".replace("__RETURN__", return_url))
        check("opening the same return address again says paid and lands on the order",
              r["url"] == f"/Orders/{second}" and r["status"] == ["Payment received. Thank you."], str(r))
        check("and changes nothing in the payments", rows(db, "select Id, Status, TxnId, PaidAt from Payments order by Id") == before)
    finally:
        h.stop_app(proc)

    # 4. Production with the simulator on: the app must refuse to start.
    prod_log = TMP / "app-prod.log"
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Production", ConnectionStrings__Default=f"Data Source={db}",
               Smtp__Host="", SeedAdmin__Email="", SeedAdmin__Password="", Payments__Mode="Simulated")
    with open(prod_log, "w") as out:
        refused = subprocess.run(["dotnet", "run", "--no-build", "--no-launch-profile", "--urls", APP],
                                 cwd=WEB, env=env, stdout=out, stderr=subprocess.STDOUT, timeout=120)
    text = prod_log.read_text()
    check("Production with Payments:Mode=Simulated refuses to start",
          refused.returncode != 0 and "refused outside Development" in text, f"exit {refused.returncode}")

    # 5. No gateway configured: cash on delivery only, no simulator.
    proc = h.start_app("Development", db, TMP / "app-off.log", {"Payments__Mode": ""})
    try:
        r = step(r"""
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
  await page.goto(APP + '__URL__');
  await page.click('[data-choice-buy]'); await page.waitForLoadState('load');
  await page.goto(APP + '/Checkout');
  const methods = await page.locator('input[name=PaymentMethod]').evaluateAll(es => es.map(e => e.value));
  const simulator = (await page.request.get(APP + '/PaymentSimulator?paymentId=1')).status();
  return JSON.stringify({ methods, simulator });
""".replace("__E__", EMAIL).replace("__P__", PASSWORD).replace("__URL__", url))
        check("without a gateway only cash on delivery is offered", r["methods"] == ["Cod"], str(r["methods"]))
        check("without a gateway the simulator does not exist", r["simulator"] == 404, str(r["simulator"]))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
