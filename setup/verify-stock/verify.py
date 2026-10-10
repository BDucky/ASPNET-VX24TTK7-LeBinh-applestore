"""Live check of stock intake (use case 20) in a real browser, against the
real app and a throwaway database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5302):
  python3 setup/verify-stock/verify.py

What it does: the seeded admin receives two products on one receipt (the
second line added with "Add a line"), the database stock and the receipt's
before/after columns agree, the shop shows the new stock; pressing save
again (browser back) saves nothing new; a product twice and a cost typed
"1.000" are refused; the receipt prints; stock levels search; an employee
can use the pages; 390px; PASS/FAIL per check.
"""
import sqlite3
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, otp_for, rows  # noqa: E402

h = Harness("verify-stock", 5302)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
ADMIN, ADMIN_PASSWORD = f"admin{STAMP}@example.com", f"Admin-{STAMP}"
STAFF, PASSWORD = f"kho{STAMP}@example.com", "Password1"


def sign_in(email, password):
    return r"""
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
""".replace("__E__", email).replace("__P__", password)


def stock(db, vid):
    return rows(db, "select StockQty from ProductVariants where Id = ?", vid)[0][0]


def main():
    db, log = TMP / "app.db", TMP / "app.log"
    h.open_browser()
    migrate(db)
    (a, a_sku), (b, b_sku) = rows(db, "select Id, SKU from ProductVariants where Status = 1 and Price is not null order by Id limit 2")
    a0, b0 = stock(db, a), stock(db, b)
    proc = h.start_app("Development", db, log, {"SeedAdmin__Email": ADMIN, "SeedAdmin__Password": ADMIN_PASSWORD})
    try:
        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.goto(APP + '/Admin/Orders');
  await press(page, 'a[href="/Admin/Stock"]');
  await press(page, 'a[href="/Admin/Stock/New"]');
  await page.fill('#Supplier', 'Công ty FPT Trading');
  await page.selectOption('select[name="Lines[0].VariantId"]', '__A__');
  await page.fill('input[name="Lines[0].Quantity"]', '10');
  await page.fill('input[name="Lines[0].UnitCost"]', '21000000');
  await page.click('[data-add-line]');
  await page.selectOption('select[name="Lines[1].VariantId"]', '__B__');
  await page.fill('input[name="Lines[1].Quantity"]', '2');
  await page.fill('input[name="Lines[1].UnitCost"]', '500000');
  await press(page, 'button:has-text("Save receipt")');
  const out = { url: page.url().replace(APP, ''), status: await text(page, '.cart-status'),
    lines: await page.locator('tr[data-receipt-line]').evaluateAll(rs => rs.map(r => Array.from(r.cells).map(c => c.textContent.trim()))) };
  // Back: the form's address holds its key, so the server shows the saved
  // receipt instead of the form; if a form did come back, press save again.
  await page.goBack();
  await page.waitForLoadState('load');
  if (await page.locator('button:has-text("Save receipt")').count()) await press(page, 'button:has-text("Save receipt")');
  out.again = { url: page.url().replace(APP, ''), status: await text(page, '.cart-status') };
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(APP + '/Admin/Stock/New');
  out.scroll = await page.evaluate(() => document.documentElement.scrollWidth);
  await page.screenshot({ path: 'new-390.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__A__", str(a)).replace("__B__", str(b)))
        check("the admin saves a two-line receipt (second line added on the page)", r["status"] == ["Receipt saved. The stock is updated."] and r["url"].startswith("/Admin/Stock/"), str(r["status"]))
        check("the database stock went up by each line's quantity", (stock(db, a), stock(db, b)) == (a0 + 10, b0 + 2), f"{stock(db, a)} {stock(db, b)}")
        line_a = next(l for l in r["lines"] if l[0] == a_sku)
        check("the receipt shows stock before and after for each line", line_a[5] == str(a0) and line_a[6] == str(a0 + 10), str(line_a))
        receipt_id = int(r["url"].split("/")[-1])
        saved = rows(db, "select VariantId, Quantity, OpeningStock, ClosingStock from StockReceiptLines where ReceiptId = ? order by VariantId", receipt_id)
        check("the stored lines agree: closing = opening + quantity", all(c == o + q for _, q, o, c in saved) and len(saved) == 2, str(saved))
        check("Back to the saved form adds nothing and shows the receipt",
              r["again"]["url"] == r["url"] and r["again"]["status"] == ["This form was already saved as a receipt, so nothing was added. To receive other goods, start a new receipt."]
              and stock(db, a) == a0 + 10 and rows(db, "select count(*) from StockReceipts")[0][0] == 1, str(r["again"]))
        check("at 390px the receipt form does not scroll sideways", r["scroll"] <= 390, str(r["scroll"]))
        check("no console errors", r["consoleErrors"] == [], str(r["consoleErrors"]))

        r = step(r"""
  const found = {};
  for (const [name, cost, second] of [['dup', '1', true], ['dots', '1.000', false]]) {
    await page.goto(APP + '/Admin/Stock/New');
    await page.fill('#Supplier', 'Test');
    await page.selectOption('select[name="Lines[0].VariantId"]', '__A__');
    await page.fill('input[name="Lines[0].Quantity"]', '1');
    await page.fill('input[name="Lines[0].UnitCost"]', cost);
    if (second) {
      await page.click('[data-add-line]');
      await page.selectOption('select[name="Lines[1].VariantId"]', '__A__');
      await page.fill('input[name="Lines[1].Quantity"]', '1');
      await page.fill('input[name="Lines[1].UnitCost"]', '1');
    }
    await press(page, 'button:has-text("Save receipt")');
    found[name] = await text(page, '.account-error');
  }
  await page.goto(APP + '/Admin/Stock/__ID__/Print');
  found.print = { nav: await page.locator('.site-nav').count(), title: (await page.locator('h1').textContent()).trim() };
  await page.goto(APP + '/Admin/Stock/Levels?q=__SKU__');
  found.levels = await page.locator('tr[data-stock-sku]').evaluateAll(rs => rs.map(r => r.getAttribute('data-stock-sku')));
  return JSON.stringify(found);
""".replace("__A__", str(a)).replace("__ID__", str(receipt_id)).replace("__SKU__", a_sku))
        check("a product twice on one receipt is refused", r["dup"] == [f"{a_sku} is on the receipt twice. Put it on one line."], str(r["dup"]))
        check("a cost typed 1.000 is refused", r["dots"] == ["Type numbers only, without dots or commas (for example 24990000)."], str(r["dots"]))
        check("the refused receipts changed no stock", stock(db, a) == a0 + 10 and rows(db, "select count(*) from StockReceipts")[0][0] == 1)
        check("the receipt prints without the shop around it", r["print"]["nav"] == 0 and r["print"]["title"] == f"Stock receipt #{receipt_id}", str(r["print"]))
        check("stock levels find the product by SKU", a_sku in r["levels"], str(r["levels"]))

        # An employee (made one in the database) can use the pages.
        step(r"""
  await signOut(page);
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: 'Kho Nhân Viên', Phone: '0977000333', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__E__", STAFF).replace("__P__", PASSWORD))
        step(r"""
  await fill(page, { Code: '__C__' }); await submit(page);
  return JSON.stringify({});
""".replace("__C__", otp_for(log, STAFF) or ""))
        with sqlite3.connect(db) as conn:
            conn.execute("update Users set Role = 1 where Email = ?", (STAFF,))
        r = step(sign_in(STAFF, PASSWORD) + r"""
  await page.goto(APP + '/Admin/Stock/New');
  await page.fill('#Supplier', 'Nhà cung cấp B');
  await page.selectOption('select[name="Lines[0].VariantId"]', '__B__');
  await page.fill('input[name="Lines[0].Quantity"]', '3');
  await page.fill('input[name="Lines[0].UnitCost"]', '400000');
  await press(page, 'button:has-text("Save receipt")');
  return JSON.stringify({ status: await text(page, '.cart-status'), by: (await page.locator('.account-lead').first().textContent()).trim() });
""".replace("__B__", str(b)))
        check("an employee receives goods and is named on the receipt", r["status"] == ["Receipt saved. The stock is updated."] and "Kho Nhân Viên" in r["by"] and stock(db, b) == b0 + 5, str(r))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
