"""Live check of batch price changes and the price history (BM_PRICE_01) in
a real browser, against the real app and a throwaway database built by the
real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5299):
  python3 setup/verify-prices/verify.py

What it does: the seeded admin raises one product's prices by 3% from
/Admin/Prices; every priced variant moves to the rounded price the database
then holds, the history lists each change with the admin's name, and the
shop shows the new price; refused batches ("1.000", a price reaching 0)
change nothing; an employee can use the page; 390px; PASS/FAIL per check.
"""
import sqlite3
import sys
import time
from decimal import ROUND_HALF_UP, Decimal
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, otp_for, rows  # noqa: E402

h = Harness("verify-prices", 5299)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
ADMIN, ADMIN_PASSWORD = f"admin{STAMP}@example.com", f"Admin-{STAMP}"
STAFF, PASSWORD = f"staff{STAMP}@example.com", "Password1"


def vnd(amount):
    return f"{int(amount):,}".replace(",", ".") + " VNĐ"


def raised(price, percent=3):
    # The app's rule: price * (100 + p) / 100, to a whole 1.000 dong, half away from zero.
    value = Decimal(price) * (100 + percent) / 100 / 1000
    return int(value.quantize(Decimal(1), rounding=ROUND_HALF_UP)) * 1000


def sign_in(email, password):
    return r"""
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
""".replace("__E__", email).replace("__P__", password)


def batch(product, mode, value):
    return r"""
  await page.goto(APP + '/Admin/Prices');
  await page.selectOption('#ProductIds', '__PID__');
  await page.selectOption('#Mode', '__MODE__');
  await page.fill('#Value', '__VALUE__');
  await press(page, 'button:has-text("Change prices")');
""".replace("__PID__", str(product)).replace("__MODE__", mode).replace("__VALUE__", value)


def main():
    db, log = TMP / "app.db", TMP / "app.log"
    h.open_browser()
    migrate(db)
    product, slug = rows(db, """
        select p.Id, p.Slug from Products p join ProductVariants v on v.ProductId = p.Id
        where p.Status = 1 and v.Status = 1 and v.Price is not null group by p.Id having count(*) > 1 order by p.SortOrder, p.Id limit 1""")[0]
    before = {vid: int(float(price)) for vid, price in rows(db, "select Id, Price from ProductVariants where ProductId = ? and Price is not null", product)}
    proc = h.start_app("Development", db, log, {"SeedAdmin__Email": ADMIN, "SeedAdmin__Password": ADMIN_PASSWORD})
    try:
        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.goto(APP + '/Admin/Orders');
  await press(page, 'a[href="/Admin/Prices"]');
""" + batch(product, "Percent", "3") + r"""
  const out = { url: page.url().replace(APP, ''), status: await text(page, '.cart-status') };
  out.rows = await page.locator('tr[data-price-change]').evaluateAll(rs => rs.map(r => Array.from(r.cells).map(c => c.textContent.trim())));
  await page.setViewportSize({ width: 390, height: 844 });
  out.scroll = await page.evaluate(() => document.documentElement.scrollWidth);
  await page.screenshot({ path: 'prices-390.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.screenshot({ path: 'prices-1440.png' });
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""")
        after = {vid: int(float(price)) for vid, price in rows(db, "select Id, Price from ProductVariants where ProductId = ? and Price is not null", product)}
        changed = [vid for vid in before if raised(before[vid]) != before[vid]]
        check("the admin's batch says how many prices changed", r["status"] == [f"Changed {len(changed)} price{'' if len(changed) == 1 else 's'}."] and r["url"] == "/Admin/Prices", str(r["status"]))
        check("every priced variant now holds the 3% price rounded to 1.000 dong", all(after[v] == raised(before[v]) for v in before), str({v: (before[v], after[v]) for v in list(before)[:3]}))
        logged = rows(db, "select VariantId, OldPrice, NewPrice, Source, ChangedByUserId from PriceChanges")
        admin_id = rows(db, "select Id from Users where Email = ?", ADMIN)[0][0]
        check("each change is logged once, as a batch, by the admin",
              sorted(v for v, *_ in logged) == sorted(changed) and all(s == 1 and u == admin_id and int(float(n)) == raised(int(float(o))) for _, o, n, s, u in logged), str(logged[:3]))
        check("the history on the page lists them with the admin's name and both prices",
              len(r["rows"]) == len(changed) and all("Batch" in row and "Admin" in " ".join(row) for row in r["rows"])
              and any(vnd(before[changed[0]]) in row and vnd(after[changed[0]]) in row for row in r["rows"]), str(r["rows"][:2]))
        check("at 390px the prices page does not scroll sideways", r["scroll"] <= 390, str(r["scroll"]))
        check("no console errors", r["consoleErrors"] == [], str(r["consoleErrors"]))

        r = step(r"""
  await page.goto(APP + '/Products/__SLUG__');
  return JSON.stringify({ prices: await page.locator('.product-grid .price').allTextContents() });
""".replace("__SLUG__", slug))
        lowest = min(after.values())
        check("the shop shows the new prices", any(vnd(lowest) in p for p in r["prices"]), str(r["prices"][:3]))

        r = step(batch(product, "Amount", "1.000") + r"""
  const out = { dots: await text(page, '.account-error') };
""" + batch(product, "Amount", f"-{max(after.values())}") + r"""
  out.zero = await text(page, '.account-error');
  return JSON.stringify(out);
""")
        check("an amount typed as 1.000 is refused, not read as one dong", r["dots"] == ["Type numbers only, without dots or commas (for example 24990000)."], str(r["dots"]))
        check("a batch that would bring a price to 0 is refused", len(r["zero"]) == 1 and "Nothing was changed." in r["zero"][0], str(r["zero"]))
        still = {vid: int(float(price)) for vid, price in rows(db, "select Id, Price from ProductVariants where ProductId = ? and Price is not null", product)}
        check("the refused batches changed no price and logged nothing", still == after and len(rows(db, "select Id from PriceChanges")) == len(changed))

        # An employee (made one in the database) can use the page.
        step(r"""
  await signOut(page);
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: 'Price Staff', Phone: '0977000222', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__E__", STAFF).replace("__P__", PASSWORD))
        step(r"""
  await fill(page, { Code: '__C__' }); await submit(page);
  return JSON.stringify({});
""".replace("__C__", otp_for(log, STAFF) or ""))
        with sqlite3.connect(db) as conn:
            conn.execute("update Users set Role = 1 where Email = ?", (STAFF,))
        r = step(sign_in(STAFF, PASSWORD) + batch(product, "Amount", "-1000") + r"""
  return JSON.stringify({ status: await text(page, '.cart-status'), by: await page.locator('tr[data-price-change]').first().innerText() });
""")
        check("an employee changes prices and is named in the history", r["status"] == [f"Changed {len(after)} price{'' if len(after) == 1 else 's'}."] and "Price Staff" in r["by"], str(r))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
