"""Live check of sideways scroll across five common breakpoints, in a real
browser, against the real app and a throwaway database built by the real
migrations.

Every other setup/verify-* script only checks 1440px and 390px. That pair
missed a real bug (found 2026-10-12): the top nav used Bootstrap's lg
collapse point (992px), but a signed-in nav (Cart, Account, Sign out, and
Staff for admins) needs up to about 1062px, so between 992px and about
1061px the page itself scrolled sideways on every signed-in and admin page.
Fixed by moving the collapse to the xl point (1200px). This script keeps
that window checked, plus two narrower bugs the same sweep found (a
multi-select sizing itself to its longest option on /Admin/Prices; a filter
bar that did not wrap on /Admin/Stock/Levels), both at 320px.

Usage (from the repo root, playwright-cli installed, nothing on port 5300):
  python3 setup/verify-responsive/verify.py

What it does:
  1. Anonymous: home, product list, a model page, Track, Compare, the two
     static pages no other script opens (Credits, Privacy), sign-in and
     register, at 320, 375, 768, 1024 and 1440px.
  2. A signed-in customer: cart, account, profile, addresses, same widths.
  3. A signed-in admin: every /Admin/* page, same widths.
  4. One check per page per width (loads, no sideways scroll), plus no
     console errors; prints PASS/FAIL per check, exits non-zero on a FAIL.
"""
import re
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, rows, scalar  # noqa: E402

h = Harness("verify-responsive", 5300)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
WIDTHS = [320, 375, 768, 1024, 1440]


def fits(results, label):
    for key, v in results.items():
        if "@" in key:
            width = int(key.split("@")[1])
            check(f"{label} page {key} fits, no sideways scroll", v["status"] == 200 and v["scroll"] <= width, str(v))
    check(f"no console errors on {label} pages across breakpoints", results["consoleErrors"] == [], str(results["consoleErrors"]))


def main():
    db, log = TMP / "app.db", TMP / "app.log"
    h.open_browser()
    migrate(db)
    proc = h.start_app("Development", db, log, {"SeedAdmin__Email": f"admin{STAMP}@example.com", "SeedAdmin__Password": f"Admin-{STAMP}"})

    product_slug = scalar(db, "select Slug from Products where Status = 1 order by Id limit 1")
    variant_slug = rows(db, """
        select p.Slug from Products p join ProductVariants v on v.ProductId = p.Id
        where v.Status = 1 and v.Price is not null and p.Status = 1 order by p.Id limit 1""")[0][0]

    try:
        r = step(r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  const out = {};
  const pages = ['/', '/Products', '/Products/__SLUG__', '/Track', '/Compare', '/Home/Credits', '/Home/Privacy', '/Account/Login', '/Account/Register'];
  for (const w of __WIDTHS__) {
    await page.setViewportSize({ width: w, height: 900 });
    for (const p of pages) {
      const resp = await page.goto(APP + p);
      out[p + '@' + w] = { status: resp.status(), scroll: await page.evaluate(() => document.documentElement.scrollWidth) };
    }
  }
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__SLUG__", product_slug).replace("__WIDTHS__", str(WIDTHS)))
        fits(r, "public")

        r2 = step(r"""
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: 'resp__S__@example.com', FullName: 'Responsive Tester', Phone: '0911222333', Password: 'Password1', ConfirmPassword: 'Password1' });
  await submit(page);
  return JSON.stringify({});
""".replace("__S__", STAMP))

        code_match = re.search(r"resp" + STAMP + r"@example\.com[^\n]*\n[^\n]*?Your code is (\d{6})", log.read_text(), re.IGNORECASE)
        otp = code_match.group(1) if code_match else ""

        r3 = step(r"""
  await fill(page, { Code: '__CODE__' }); await submit(page);
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.goto(APP + '/Products/__VSLUG__');
  await press(page, 'a[href*="/Products/__VSLUG__/"]');
  await press(page, '[data-choice-buy]');
  const out = {};
  const pages = ['/Cart', '/Account', '/Account/Profile', '/Account/Addresses'];
  for (const w of __WIDTHS__) {
    await page.setViewportSize({ width: w, height: 900 });
    for (const p of pages) {
      const resp = await page.goto(APP + p);
      out[p + '@' + w] = { status: resp.status(), scroll: await page.evaluate(() => document.documentElement.scrollWidth) };
    }
  }
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__CODE__", otp).replace("__VSLUG__", variant_slug).replace("__WIDTHS__", str(WIDTHS)))
        fits(r3, "signed-in")

        r4 = step(r"""
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: 'admin__S__@example.com', Password: 'Admin-__S__' }); await submit(page);
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  const out = {};
  const pages = ['/Admin', '/Admin/Products', '/Admin/Vouchers', '/Admin/Promotions', '/Admin/Users', '/Admin/Orders',
                 '/Admin/Reports', '/Admin/Prices', '/Admin/Stock', '/Admin/Stock/Levels', '/Admin/Stock/New', '/Admin/Sales/New'];
  for (const w of __WIDTHS__) {
    await page.setViewportSize({ width: w, height: 900 });
    for (const p of pages) {
      const resp = await page.goto(APP + p);
      out[p + '@' + w] = { status: resp.status(), scroll: await page.evaluate(() => document.documentElement.scrollWidth) };
    }
  }
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__S__", STAMP).replace("__WIDTHS__", str(WIDTHS)))
        fits(r4, "admin")
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
