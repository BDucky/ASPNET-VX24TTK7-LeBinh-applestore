"""Live check of in-person sales (use case 21) in a real browser, against the
real app and a throwaway database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5303):
  python3 setup/verify-sales/verify.py

What it does: the seeded admin opens a counter sale from the orders page,
prices two products (the second line added with "Add a line"), completes the
sale in cash; the database has a paid, completed in-store order at that total
and the stock went down; Back shows the sale and sells nothing more; a second
sale with a voucher takes the voucher's discount; the revenue report counts
both; two invoices print together; 390px; PASS/FAIL per check.
"""
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, rows  # noqa: E402

h = Harness("verify-sales", 5303)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
ADMIN, ADMIN_PASSWORD = f"admin{STAMP}@example.com", f"Admin-{STAMP}"


def vnd(amount):
    return f"{int(amount):,}".replace(",", ".") + " VNĐ"


def stock(db, vid):
    return rows(db, "select StockQty from ProductVariants where Id = ?", vid)[0][0]


def sale_steps(lines, voucher=""):
    js = r"""
  await page.goto(APP + '/Admin/Orders');
  await press(page, 'a[href="/Admin/Sales/New"]');
  const formUrl = page.url().replace(APP, '');
"""
    for i, (vid, qty) in enumerate(lines):
        if i > 0:
            js += "  await page.click('[data-add-line]');\n"
        js += f"  await page.selectOption('select[name=\"Lines[{i}].VariantId\"]', '{vid}');\n"
        js += f"  await page.fill('input[name=\"Lines[{i}].Quantity\"]', '{qty}');\n"
    js += f"  await page.fill('#VoucherCode', '{voucher}');\n"
    js += r"""
  await press(page, 'button[value=quote]');
  const total = (await page.locator('[data-sale-total]').textContent()).trim();
  await press(page, 'button[value=sell]');
"""
    return js


def main():
    db, log = TMP / "app.db", TMP / "app.log"
    h.open_browser()
    migrate(db)
    (a, a_price), (b, b_price) = [(v, int(float(p))) for v, p in rows(db, """
        select v.Id, v.Price from ProductVariants v join Products p on p.Id = v.ProductId
        where v.Status = 1 and p.Status = 1 and v.Price is not null and v.StockQty > 5 order by v.Id limit 2""")]
    a0, b0 = stock(db, a), stock(db, b)
    proc = h.start_app("Development", db, log, {"SeedAdmin__Email": ADMIN, "SeedAdmin__Password": ADMIN_PASSWORD})
    try:
        r = step(r"""
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
""".replace("__E__", ADMIN).replace("__P__", ADMIN_PASSWORD) + sale_steps([(a, 1), (b, 2)]) + r"""
  const out = { formUrl, total, url: page.url().replace(APP, ''), status: await text(page, '.cart-status'), lead: (await page.locator('.account-lead').first().textContent()).trim() };
  await page.goto(APP + formUrl);
  out.back = { url: page.url().replace(APP, ''), status: await text(page, '.cart-status') };
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(APP + '/Admin/Sales/New');
  out.scroll = await page.evaluate(() => document.documentElement.scrollWidth);
  await page.screenshot({ path: 'sale-390.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""")
        expected = a_price + 2 * b_price
        check("the priced total is the sum of the database prices", r["total"] == f"Total {vnd(expected)}", f"{r['total']} vs {vnd(expected)}")
        check("completing the sale lands on the order, marked in store and sold by the admin",
              r["status"] == ["Sale completed and paid."] and r["url"].startswith("/Admin/Orders/") and "In store" in r["lead"] and "Sold by" in r["lead"], str(r))
        order = int(r["url"].split("/")[-1])
        row = rows(db, "select Channel, Status, PaymentStatus, TotalAmount, UserId from Orders where Id = ?", order)[0]
        check("the database has a paid, completed in-store order at that total, with no account", row[:3] == (1, 3, 1) and int(float(row[3])) == expected and row[4] is None, str(row))
        check("the stock went down by what was sold", (stock(db, a), stock(db, b)) == (a0 - 1, b0 - 2), f"{stock(db, a)} {stock(db, b)}")
        check("the payment is cash, paid in full", rows(db, "select Method, Status, PaidAmount from Payments where OrderId = ?", order)[0][:2] == (3, 1))
        check("Back to the sale form shows the sale and sells nothing more",
              r["back"]["url"] == r["url"] and r["back"]["status"] == ["This form was already used for a sale, so nothing more was sold. To sell again, start a new sale."]
              and stock(db, a) == a0 - 1, str(r["back"]))
        check("at 390px the sale form does not scroll sideways", r["scroll"] <= 390, str(r["scroll"]))
        check("no console errors", r["consoleErrors"] == [], str(r["consoleErrors"]))

        r = step(sale_steps([(a, 1)], "WELCOME10") + r"""
  return JSON.stringify({ total, url: page.url().replace(APP, '') });
""")
        second = int(r["url"].split("/")[-1])
        discount = int(float(rows(db, "select DiscountAmount from Orders where Id = ?", second)[0][0]))
        check("a voucher at the counter is taken off and counted", discount > 0 and r["total"] == f"Total {vnd(a_price - discount)}"
              and rows(db, "select UsedCount from Vouchers where Code = 'WELCOME10'")[0][0] == 1, f"{r['total']} discount {discount}")

        r = step(r"""
  await page.goto(APP + '/Admin/Reports');
  const revenue = (await page.locator('[data-report-revenue]').textContent()).trim();
  await page.goto(APP + '/Admin/Orders');
  await page.check('input[name=ids][value="__A__"]');
  await page.check('input[name=ids][value="__B__"]');
  const [popup] = await Promise.all([page.waitForEvent('popup'), page.click('button:has-text("Print selected invoices")')]);
  await popup.waitForLoadState('load');
  const out = { revenue, invoices: await popup.locator('[data-invoice]').evaluateAll(es => es.map(e => e.getAttribute('data-invoice'))),
    vat: (await popup.locator('body').innerText()).includes('Prices include VAT.'), nav: await popup.locator('.site-nav').count() };
  await popup.close();
  return JSON.stringify(out);
""".replace("__A__", str(order)).replace("__B__", str(second)))
        total_two = expected + a_price - discount
        check("the revenue report counts both counter sales", r["revenue"] == vnd(total_two), f"{r['revenue']} vs {vnd(total_two)}")
        check("two ticked invoices print together, with the VAT note and no shop around them",
              sorted(r["invoices"]) == sorted([str(order), str(second)]) and r["vat"] and r["nav"] == 0, str(r))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
