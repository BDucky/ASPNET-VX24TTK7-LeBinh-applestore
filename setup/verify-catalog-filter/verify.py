"""Live check of the product filters (use case 8) in a real browser, against
the real app and a throwaway database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5296):
  python3 setup/verify-catalog-filter/verify.py

What it does: on the full product list, each price band shows exactly the
products the database says have an active variant priced inside it, each
card's price is inside the band; sorting and searching keep the band; "All
prices" clears it; "Newest" puts the latest added first; 390px; PASS/FAIL.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, rows  # noqa: E402

h = Harness("verify-catalog-filter", 5296)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step

# Same table as PriceBands in the app: min included, max not.
BANDS = {
    "Under10M": (None, 10_000_000),
    "From10MTo20M": (10_000_000, 20_000_000),
    "From20MTo40M": (20_000_000, 40_000_000),
    "Over40M": (40_000_000, None),
}


def in_band(db, band, query=None):
    lo, hi = BANDS[band]
    # Prices are TEXT in SQLite: compare as numbers.
    sql = """select distinct p.Name from Products p join ProductVariants v on v.ProductId = p.Id and v.Status = 1
             where p.Status = 1 and v.Price is not null"""
    args = []
    if lo is not None:
        sql += " and cast(v.Price as real) >= ?"
        args.append(lo)
    if hi is not None:
        sql += " and cast(v.Price as real) < ?"
        args.append(hi)
    if query:
        sql += " and p.Name like ?"
        args.append(f"%{query}%")
    return sorted(r[0] for r in rows(db, sql, *args))


CARDS = r"""
  const cards = await page.locator('.product-grid .product-card').evaluateAll(cs => cs.map(c => ({
    name: c.querySelector('h3').textContent.trim(),
    price: Number((c.querySelector('.price').textContent.match(/[\d.]+(?= VNĐ)/) || ['0'])[0].replace(/\./g, '')),
  })));
"""


def main():
    db, log = TMP / "app.db", TMP / "app.log"
    h.open_browser()
    migrate(db)
    newest = rows(db, "select Name from Products where Status = 1 order by CreatedAt desc, Id desc limit 1")[0][0]
    total = rows(db, "select count(*) from Products where Status = 1")[0][0]
    proc = h.start_app("Development", db, log)
    try:
        r = step(r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  const out = { bands: {} };
  for (const band of ['Under10M', 'From10MTo20M', 'From20MTo40M', 'Over40M']) {
    await page.goto(APP + '/Products');
    await press(page, '[data-price-band="' + band + '"]');
""" + CARDS + r"""
    out.bands[band] = { url: page.url().replace(APP, ''), cards, active: await page.locator('[data-price-band][aria-current="true"]').getAttribute('data-price-band') };
  }
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""")
        for band, (lo, hi) in BANDS.items():
            got = r["bands"][band]
            names = sorted(c["name"] for c in got["cards"])
            check(f"{band}: the products the database has in the band", names == in_band(db, band), f"{len(names)} shown, {len(in_band(db, band))} expected")
            check(f"{band}: every card price is inside the band and the band is marked",
                  all((lo is None or c["price"] >= lo) and (hi is None or c["price"] < hi) for c in got["cards"]) and got["active"] == band,
                  str([c for c in got["cards"] if not ((lo is None or c["price"] >= lo) and (hi is None or c["price"] < hi))][:3]))
        check("no console errors", r["consoleErrors"] == [], str(r["consoleErrors"]))

        r = step(r"""
  await page.goto(APP + '/Products?band=From20MTo40M');
  await Promise.all([page.waitForNavigation(), page.selectOption('#sort-select', 'PriceAscending')]);
""" + CARDS + r"""
  const out = { sortUrl: page.url().replace(APP, ''), sorted: cards.map(c => c.price) };
  await page.fill('.product-search-input', 'iPhone');
  await press(page, '.product-search button');
  out.searchUrl = decodeURIComponent(page.url().replace(APP, ''));
  out.searched = (await page.locator('.product-grid h3').allTextContents()).map(s => s.trim()).sort();
  await press(page, '[data-price-band="Any"]');
  out.allUrl = page.url().replace(APP, '');
  out.allCount = await page.locator('.product-grid .product-card').count();
  return JSON.stringify(out);
""")
        prices = r["sorted"]
        check("sorting keeps the band, and prices go up", "band=From20MTo40M" in r["sortUrl"] and prices == sorted(prices) and len(prices) > 0, r["sortUrl"])
        check("searching keeps the band, and shows only matches in it",
              "band=From20MTo40M" in r["searchUrl"] and r["searched"] == in_band(db, "From20MTo40M", "iPhone"), r["searchUrl"])
        check("All prices clears the band and keeps the search", "band=" not in r["allUrl"] and "q=iPhone" in r["allUrl"], r["allUrl"])

        r = step(r"""
  await page.goto(APP + '/Products');
  const out = { all: await page.locator('.product-grid .product-card').count() };
  await Promise.all([page.waitForNavigation(), page.selectOption('#sort-select', 'Newest')]);
  out.first = (await page.locator('.product-grid h3').first().textContent()).trim();
  out.url = page.url().replace(APP, '');
  await page.goto(APP + '/Products?band=99');
  out.junk = await page.locator('.product-grid .product-card').count();
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto(APP + '/Products?band=Over40M');
  out.scroll = await page.evaluate(() => document.documentElement.scrollWidth);
  await page.locator('.listing-toolbar').screenshot({ path: 'toolbar-390.png' });
  await page.screenshot({ path: 'bands-390.png' });
  await page.setViewportSize({ width: 1440, height: 900 });
  // Reload and let the cards fade in: a shot taken mid-resize shows one card.
  await page.reload();
  await page.waitForTimeout(1500);
  await page.screenshot({ path: 'bands-1440.png' });
  return JSON.stringify(out);
""")
        check("the full list shows every product on sale", r["all"] == total, f"{r['all']} of {total}")
        check("Newest puts the latest added product first", r["first"] == newest and "sort=Newest" in r["url"], f"{r['first']} vs {newest}")
        check("a hand-edited band lists everything", r["junk"] == total, str(r["junk"]))
        check("at 390px the bands wrap without sideways scroll", r["scroll"] <= 390, str(r["scroll"]))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
