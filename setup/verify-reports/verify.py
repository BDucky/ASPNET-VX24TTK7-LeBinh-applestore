"""Live check of the sales report, its Excel export, the print pages and the
invoices (use cases 33-36) in a real browser, against the real app and a
throwaway database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5293):
  python3 setup/verify-reports/verify.py

What it does:
  1. Seeds an admin (environment variables), registers a customer, places two
     cash orders; the admin confirms, ships and completes the first.
  2. The report counts only the delivered (paid) order; the second shows as
     waiting. Downloads the Excel file and opens it (it is a zip with three
     sheets). The print page has no shop navigation.
  3. Invoice: the customer's own (and printed to a real PDF by Chromium),
     another customer gets a 404, staff print any.
  4. PASS/FAIL per check, non-zero exit on any FAIL.
"""
import re
import sys
import time
import zipfile
from decimal import Decimal
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, otp_for, rows, scalar  # noqa: E402

h = Harness("verify-reports", 5293)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
ADMIN, ADMIN_PASSWORD = f"admin{STAMP}@example.com", f"Admin-{STAMP}"
CUSTOMER, OTHER, PASSWORD = f"buyer{STAMP}@example.com", f"other{STAMP}@example.com", "Password1"
SEED = {"SeedAdmin__Email": ADMIN, "SeedAdmin__Password": ADMIN_PASSWORD}


def vnd(amount):
    return f"{int(amount):,}".replace(",", ".") + " VNĐ"


def sign_in(email, password):
    return r"""
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
""".replace("__E__", email).replace("__P__", password)


def register(log, email, name, phone):
    step(r"""
  await signOut(page);
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: '__N__', Phone: '__PH__', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__E__", email).replace("__N__", name).replace("__P__", PASSWORD).replace("__PH__", phone))
    return step(r"""
  await fill(page, { Code: '__C__' }); await submit(page);
  return JSON.stringify({ url: page.url().replace(APP, '') });
""".replace("__C__", otp_for(log, email) or ""))["url"]


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
    proc = h.start_app("Development", db, log, SEED)
    try:
        url = h.config_url(slug, config)
        check("two customers registered", register(log, CUSTOMER, "Report Buyer", "0977000111") == "/Account" and register(log, OTHER, "Other Buyer", "0977000222") == "/Account")

        # 1. Two cash orders; the first is delivered.
        place = r"""
  await page.goto(APP + '__URL__');
  await press(page, '[data-choice-buy]');
  await page.goto(APP + '/Checkout');
  await page.fill('#AddressLine', '7 Report Street');
  await press(page, 'button[value=place]');
""".replace("__URL__", url)
        r = step(sign_in(CUSTOMER, PASSWORD) + place + "const first = page.url().replace(APP, '');" + place
                 + "return JSON.stringify({ first, second: page.url().replace(APP, '') });")
        first, second = int(r["first"].split("/")[-1]), int(r["second"].split("/")[-1])
        price = int(Decimal(scalar(db, "select TotalAmount from Orders where Id = ?", first)))
        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  await page.goto(APP + '/Admin/Orders/__ID__');
  await press(page, 'button[value=Confirm]');
  await page.fill('#Carrier', 'GHN'); await page.fill('#TrackingNo', 'R-1');
  await press(page, 'button[value=Ship]');
  await press(page, 'button[value=Complete]');
  return JSON.stringify({ done: await text(page, '.cart-status') });
""".replace("__ID__", str(first)))
        check("the first order was delivered (cash paid)", r["done"] == ["Order completed."] and scalar(db, "select PaymentStatus from Orders where Id = ?", first) == 1, str(r))

        # 2. The report, the Excel file, the print page.
        r = step(r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.goto(APP + '/Admin/Orders');
  await press(page, 'a[href="/Admin/Reports"]');
  const out = { revenue: await page.locator('[data-report-revenue]').innerText(), hint: await page.locator('.account-hint').first().innerText(),
                products: await page.locator('.report-table').last().innerText() };
  const [download] = await Promise.all([page.waitForEvent('download'), page.click('text=Download Excel')]);
  out.file = download.suggestedFilename();
  await download.saveAs('sales.xlsx');
  await page.screenshot({ path: 'report-1440.png', fullPage: true });
  // Every amount inside its card, and the shop nav on one row (both seen
  // broken in the report's screenshots, 2026-10-09).
  const fits = () => page.evaluate(() => Array.from(document.querySelectorAll('.admin-stat dd')).every(dd =>
    dd.getBoundingClientRect().right <= dd.closest('.admin-stat').getBoundingClientRect().right + 1 && dd.scrollWidth <= dd.clientWidth + 1));
  out.fits1440 = await fits();
  out.navRows = await page.evaluate(() => new Set(Array.from(document.querySelectorAll('.site-nav-links .nav-link')).map(a => Math.round(a.getBoundingClientRect().top))).size);
  await page.setViewportSize({ width: 390, height: 844 });
  await page.reload();
  out.scroll = await page.evaluate(() => document.documentElement.scrollWidth);
  out.fits390 = await fits();
  await page.screenshot({ path: 'report-390.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  const print = await page.context().newPage();
  await print.goto(APP + '/Admin/Reports/Print');
  out.printNav = await print.locator('.site-nav').count();
  out.printTitle = await print.locator('h1').innerText();
  await print.close();
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""")
        check("the report counts only the delivered order", r["revenue"] == vnd(price), f"{r['revenue']} vs {vnd(price)}")
        check("the waiting order is shown for reference, not counted", "1 waiting for payment" in r["hint"], r["hint"])
        check("the product table names the watch", config.split(" ")[0] in r["products"] or "Apple Watch" in r["products"], r["products"][:120])
        with zipfile.ZipFile(TMP / "sales.xlsx") as workbook:
            names = re.findall(r'<(?:\w+:)?sheet name="([^"]+)"', workbook.read("xl/workbook.xml").decode())
        check("the Excel download is a real workbook with three sheets", r["file"].startswith("sales-") and r["file"].endswith(".xlsx") and names == ["Summary", "By day", "By product"], f"{r['file']} {names}")
        check("the report fits 390px", r["scroll"] <= 390, str(r["scroll"]))
        check("every amount stays inside its card at 1440px and 390px", r["fits1440"] and r["fits390"], f"{r['fits1440']} {r['fits390']}")
        check("the shop nav stays on one row with an admin signed in", r["navRows"] == 1, str(r["navRows"]))
        check("the print page has no shop navigation", r["printNav"] == 0 and r["printTitle"] == "Sales report", str(r))
        check("no console errors on the report", r["consoleErrors"] == [], str(r["consoleErrors"]))

        # 3. Invoices.
        r = step(sign_in(CUSTOMER, PASSWORD) + r"""
  await page.goto(APP + '/Orders/__ID__');
  const out = { link: await page.locator('a[href="/Orders/__ID__/Invoice"]').count() };
  const invoice = await page.context().newPage();
  await invoice.goto(APP + '/Orders/__ID__/Invoice');
  out.title = await invoice.locator('h1').innerText();
  out.nav = await invoice.locator('.site-nav').count();
  await invoice.emulateMedia({ media: 'print' });
  out.buttonPrinted = await invoice.locator('.print-actions').isVisible();
  await invoice.pdf({ path: 'invoice.pdf', format: 'A4' });
  await invoice.close();
  return JSON.stringify(out);
""".replace("__ID__", str(first)))
        pdf = (TMP / "invoice.pdf").read_bytes() if (TMP / "invoice.pdf").exists() else b""
        check("the customer opens and prints their invoice", r["link"] == 1 and r["title"] == f"Invoice for order #{first}" and r["nav"] == 0, str(r))
        check("the print button is hidden on paper", r["buttonPrinted"] is False, str(r["buttonPrinted"]))
        check("the invoice prints to a real PDF", pdf.startswith(b"%PDF") and len(pdf) > 1000, f"{len(pdf)} bytes")
        r = step(sign_in(OTHER, PASSWORD) + r"""
  const resp = await page.request.get(APP + '/Orders/__ID__/Invoice');
  return JSON.stringify({ status: resp.status() });
""".replace("__ID__", str(first)))
        check("another customer gets a 404 for that invoice", r["status"] == 404, str(r))
        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  const resp = await page.request.get(APP + '/Admin/Orders/__ID__/Invoice');
  return JSON.stringify({ status: resp.status(), body: (await resp.text()).includes('Invoice for order #__ID__') });
""".replace("__ID__", str(second)))
        check("staff print any invoice", r["status"] == 200 and r["body"], str(r))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
