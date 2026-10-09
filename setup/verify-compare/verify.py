"""Live check of compare products (use case 10) in a real browser, against
the real app and a throwaway database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5295):
  python3 setup/verify-compare/verify.py

What it does: a visitor (no account) adds three products from three
categories, is refused a fourth, sees them side by side with the prices the
database holds, adds from a configuration page after picking a colour and
comes back to that colour, removes and clears; a product hidden while in the
list drops out; a hand-edited cookie; 390px; PASS/FAIL per check.
"""
import sqlite3
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, rows  # noqa: E402

h = Harness("verify-compare", 5295)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step


def vnd(price):
    return f"{int(round(float(price))):,}".replace(",", ".") + " VNĐ"


def main():
    db, log = TMP / "app.db", TMP / "app.log"
    h.open_browser()
    migrate(db)
    # One priced, visible product from each of four categories, catalog order.
    # Prices are TEXT in SQLite: min() needs the cast or it compares strings.
    picks = rows(db, """
        select p.Id, p.Slug, p.Name, c.Slug, min(cast(v.Price as real)) from Products p join Categories c on c.Id = p.CategoryId
        join ProductVariants v on v.ProductId = p.Id and v.Status = 1 and v.Price is not null
        where p.Status = 1 and c.Slug in ('iphone', 'mac', 'airpods', 'watch')
        group by p.Id order by p.SortOrder, p.Id""")
    by_cat = {}
    for pid, slug, name, cat, price in picks:
        by_cat.setdefault(cat, (pid, slug, name, price))
    a, b, c, d = by_cat["iphone"], by_cat["mac"], by_cat["airpods"], by_cat["watch"]
    # A configuration of the watch with at least two colours, for the colour check.
    config = rows(db, """
        select ov.Value from ProductVariants v
        join VariantOptions vo on vo.VariantId = v.Id join OptionTypes ot on ot.Id = vo.OptionTypeId and ot.Code = 'config'
        join OptionValues ov on ov.Id = vo.OptionValueId
        join VariantOptions vc on vc.VariantId = v.Id join OptionTypes oc on oc.Id = vc.OptionTypeId and oc.Code = 'color'
        where v.ProductId = ? and v.Status = 1 group by ov.Value having count(distinct vc.OptionValueId) > 1 order by ov.Value limit 1""", d[0])[0][0]
    proc = h.start_app("Development", db, log)
    try:
        watch_config = h.config_url(d[1], config)
        r = step(r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  const out = { notes: [], nav: [] };
  for (const slug of ['__A__', '__B__', '__C__', '__D__']) {
    await page.goto(APP + '/Products/' + slug);
    await press(page, '.compare-add button');
    out.notes.push((await text(page, '.cart-status, .cart-error')).join(' '));
    out.nav.push((await text(page, '[data-compare-count]')).join(' '));
    out.last = page.url().replace(APP, '');
  }
  await page.goto(APP + '/Compare');
  out.columns = await page.locator('[data-compare-product]').evaluateAll(ths => ths.map(t => t.getAttribute('data-compare-product')));
  out.table = await page.locator('[data-compare-table]').innerText();
  await page.screenshot({ path: 'compare-1440.png', fullPage: true });
  await page.setViewportSize({ width: 390, height: 844 });
  out.scroll = await page.evaluate(() => document.documentElement.scrollWidth);
  out.tableScrolls = await page.locator('.compare-scroll').evaluate(e => e.scrollWidth > e.clientWidth);
  await page.screenshot({ path: 'compare-390.png', fullPage: true });
  await page.setViewportSize({ width: 1440, height: 900 });
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__A__", a[1]).replace("__B__", b[1]).replace("__C__", c[1]).replace("__D__", d[1]))
        check("a visitor adds three products from three categories", r["notes"][:3] == ["Added to compare."] * 3 and r["nav"][:3] == ["Compare (1)", "Compare (2)", "Compare (3)"], str(r["notes"]) + str(r["nav"]))
        check("the fourth is refused with a message, back on its page", r["notes"][3] == "You can compare up to 3 products. Remove one first." and r["last"] == f"/Products/{d[1]}" and r["nav"][3] == "Compare (3)", str(r))
        check("the compare page shows the three in the order added", r["columns"] == [str(a[0]), str(b[0]), str(c[0])], str(r["columns"]))
        check("each column shows the lowest active price in the database", all(vnd(p[3]) in r["table"] for p in (a, b, c)), r["table"][:300])
        check("no reviews yet shows as such, not as 0", r["table"].count("No reviews yet") == 3)
        check("at 390px the page does not scroll sideways, the table does", r["scroll"] <= 390 and r["tableScrolls"], f"{r['scroll']} {r['tableScrolls']}")
        check("no console errors", r["consoleErrors"] == [], str(r["consoleErrors"]))

        r = step(r"""
  await page.goto(APP + '/Compare');
  await press(page, '[data-compare-product="__B__"] button');
  const out = { afterRemove: await page.locator('[data-compare-product]').count() };
  await page.goto(APP + '__URL__');
  const chips = page.locator('[data-choice-color]:not(.is-selected)');
  const color = await chips.first().getAttribute('data-choice-color');
  await chips.first().click();
  await press(page, '.compare-add button');
  out.color = color;
  out.back = decodeURIComponent(page.url().replace(APP, ''));
  out.note = (await text(page, '.cart-status')).join(' ');
  return JSON.stringify(out);
""".replace("__B__", str(b[0])).replace("__URL__", watch_config))
        check("remove takes one column out", r["afterRemove"] == 2, str(r))
        check("adding from a configuration page comes back to the colour picked", r["note"] == "Added to compare." and f"color={r['color']}" in r["back"].replace("+", " "), str(r))

        with sqlite3.connect(db) as conn:
            conn.execute("update Products set Status = 0 where Id = ?", (a[0],))
        r = step(r"""
  await page.goto(APP + '/Compare');
  const out = { columns: await page.locator('[data-compare-product]').evaluateAll(ths => ths.map(t => t.getAttribute('data-compare-product'))) };
  await page.goto(APP + '/');
  out.nav = (await text(page, '[data-compare-count]')).join(' ');
  return JSON.stringify(out);
""")
        check("a product hidden while in the list drops out, and the nav count follows", r["columns"] == [str(c[0]), str(d[0])] and r["nav"] == "Compare (2)", str(r))

        r = step(r"""
  await page.goto(APP + '/Compare');
  await press(page, '.compare-clear button');
  const out = { empty: (await page.locator('.account-card').innerText()).includes('Nothing to compare yet'), nav: await page.locator('[data-compare-count]').count() };
  await page.context().addCookies([{ name: 'AppleStore.Compare', value: 'abc.-1.999999.__C__.__C__', url: APP }]);
  const resp = await page.goto(APP + '/Compare');
  out.status = resp.status();
  out.tampered = await page.locator('[data-compare-product]').evaluateAll(ths => ths.map(t => t.getAttribute('data-compare-product')));
  return JSON.stringify(out);
""".replace("__C__", str(c[0])))
        check("clear empties the list and the nav link goes", r["empty"] and r["nav"] == 0, str(r))
        check("a hand-edited cookie still opens the page with only the real product", r["status"] == 200 and r["tampered"] == [str(c[0])], str(r))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
