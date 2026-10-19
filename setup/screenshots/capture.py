"""Screenshots of the main pages for the report and slides (task 11), taken
from the real app on a fresh database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5300):
  python3 setup/screenshots/capture.py

The script plays a customer and the admin first (orders, a delivered order, a
promotion, a review, a stock receipt, a counter sale), then takes each shot
in a fresh 2x browser context with the same sign-in, cropped to the block
that matters (a card, the product panel, a table), so text stays readable
when the image is enlarged on a slide or printed in the report (owner agreed
2026-10-09 that whole-page shots were too small). Most shots also get a
companion `<name>-full.png`: the whole page, 2x, with the cropped block
outlined in red, so the report can show real page context next to the
readable detail crop (owner, 2026-10-17, after the cropped-only shots made
the page impossible to picture as a whole). Writes PNG files to
thesis/doc/hinh/. Re-run after a UI change.
"""
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import ROOT, Harness, migrate, otp_for, rows  # noqa: E402

h = Harness("screenshots", 5300)
APP, TMP = h.app, h.tmp
OUT = ROOT / "thesis" / "doc" / "hinh"
STAMP = str(int(time.time()))
ADMIN, ADMIN_PASSWORD = f"admin{STAMP}@example.com", f"Admin-{STAMP}"
CUSTOMER, PASSWORD = f"khach{STAMP}@example.com", "Password1"

# The site's custom cursor ring would show in every shot.
HIDE = "await p.addStyleTag({ content: '.cursor-ring, .cursor-dot { display: none !important; }' });"


def step(body):
    return h.step(body)


# A fake browser chrome bar (traffic-light dots + an address bar showing the
# real URL), prepended as a normal (not fixed) block so it pushes the page
# down and appears once in a `fullPage` shot. Inserted before the highlight
# box is measured, so the box lands at the right (post-shift) coordinates.
# Without this a full-page shot is still just "a dark rectangle of UI" with
# nothing to anchor it as a real site at a real address (owner, 2026-10-17).
CHROME_JS = (
    "await p.evaluate(() => {"
    "const bar = document.createElement('div');"
    "bar.style.cssText = 'all:unset;display:flex;align-items:center;width:100%;height:40px;"
    "background:#e8e8ec;padding:0 14px;box-sizing:border-box;border-bottom:1px solid #c7c7cc;"
    "font-family:-apple-system,Helvetica,Arial,sans-serif;';"
    "const dots = document.createElement('div');"
    "dots.style.cssText = 'all:unset;display:flex;gap:7px;margin-right:16px;flex:none;';"
    "for (const c of ['#ff5f57','#febc2e','#28c840']) {"
    "const d = document.createElement('span');"
    "d.style.cssText = 'all:unset;display:inline-block;width:12px;height:12px;border-radius:50%;background:' + c + ';';"
    "dots.appendChild(d);"
    "}"
    "const addr = document.createElement('div');"
    "addr.style.cssText = 'all:unset;flex:1;display:block;background:#fff;border-radius:6px;"
    "padding:7px 12px;font-size:14px;line-height:1.2;color:#1d1d1f;border:1px solid #d0d0d5;"
    "white-space:nowrap;overflow:hidden;text-overflow:ellipsis;';"
    "addr.textContent = location.href;"
    "bar.appendChild(dots);"
    "bar.appendChild(addr);"
    "document.body.insertBefore(bar, document.body.firstChild);"
    "});"
)

# Outlines `selector` in red, in page (not viewport) coordinates, so the box
# lands in the right place in a `fullPage` screenshot regardless of scroll.
# Capped at 3x the viewport height: an uncapped `fullPage` shot of a page with
# a long list (stock levels was 80.752px tall) shrinks to an unreadable sliver
# once scaled to the report's column width (the same failure mode the 2026-
# 10-10 session hit with whole-page shots generally, before cropping; capping
# the height here keeps this specific companion shot safe without reverting
# to a full crop).
HIGHLIGHT_JS = (
    CHROME_JS +
    "const __hb = await p.locator('__SEL__').first().boundingBox();"
    "await p.evaluate((r) => {"
    "const d = document.createElement('div');"
    "d.style.cssText = 'position:absolute;left:' + (r.x - 8 + window.scrollX) + 'px;top:' + "
    "(r.y - 8 + window.scrollY) + 'px;width:' + (r.width + 16) + 'px;height:' + (r.height + 16) + "
    "'px;border:5px solid #ff2d2d;border-radius:10px;box-shadow:0 0 0 2px rgba(255,45,45,.35);"
    "pointer-events:none;z-index:2147483647;box-sizing:border-box;';"
    "document.body.appendChild(d);"
    "}, __hb);"
    "const __sh = await p.evaluate(() => document.documentElement.scrollHeight);"
    "await p.screenshot({ path: '__FULLPNG__', fullPage: true,"
    " clip: { x: 0, y: 0, width: 1440, height: Math.min(__sh, 2700) } });"
)


def shot(name, path, selector=None, before="", wait=1500, full=False, top=None, highlight=False):
    """A 2x shot of `path` with the current sign-in; cropped to `selector`.

    `top` keeps only the first `top` CSS pixels of a long element (a list of
    every variant is thousands of pixels tall and shrinks to a sliver in the
    report). `highlight` (needs `selector`) also takes a companion full-page
    shot (`<name>-full.png`) with `selector` outlined in red, so the report
    can show the whole page for context next to the cropped detail."""
    png = OUT / (name + '.png')
    highlight_js = ""
    if highlight and selector:
        full_png = OUT / (name + '-full.png')
        highlight_js = HIGHLIGHT_JS.replace("__SEL__", selector).replace("__FULLPNG__", str(full_png))
    if selector and top:
        target = (f"const b = await p.locator('{selector}').first().boundingBox();"
                  f" await p.screenshot({{ path: '{png}', fullPage: true,"
                  f" clip: {{ x: b.x, y: b.y, width: b.width, height: Math.min(b.height, {top}) }} }});")
    elif selector:
        target = f"await p.locator('{selector}').first().screenshot({{ path: '{png}' }});"
    else:
        target = f"await p.screenshot({{ path: '{png}', fullPage: {'true' if full else 'false'} }});"
    step(r"""
  const ctx = await page.context().browser().newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2, storageState: await page.context().storageState() });
  const p = await ctx.newPage();
  try {
    await p.goto(APP + '__PATH__');
    __BEFORE__
    __HIDE__
    await p.waitForTimeout(__WAIT__);
    __HIGHLIGHT__
    __TARGET__
  } finally {
    await ctx.close();
  }
  return JSON.stringify({});
""".replace("__PATH__", path).replace("__BEFORE__", before).replace("__HIDE__", HIDE)
        .replace("__WAIT__", str(wait)).replace("__HIGHLIGHT__", highlight_js).replace("__TARGET__", target))


def sign_in(email, password):
    step(r"""
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
  return JSON.stringify({});
""".replace("__E__", email).replace("__P__", password))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    db, log = TMP / "app.db", TMP / "app.log"
    h.open_browser()
    migrate(db)
    slug, config = rows(db, """
        select p.Slug, ov.Value from ProductVariants v join Products p on p.Id = v.ProductId
        join VariantOptions vo on vo.VariantId = v.Id join OptionTypes ot on ot.Id = vo.OptionTypeId and ot.Code = 'config'
        join OptionValues ov on ov.Id = vo.OptionValueId
        where p.Slug like 'iphone-18-pro%' and v.Status = 1 and v.Price is not null and v.StockQty > 3 order by p.SortOrder limit 1""")[0]
    (va, _), (vb, _) = rows(db, """
        select v.Id, v.SKU from ProductVariants v join Products p on p.Id = v.ProductId
        where v.Status = 1 and p.Status = 1 and v.Price is not null and v.StockQty > 5 order by v.Id limit 2""")
    proc = h.start_app("Development", db, log, {"SeedAdmin__Email": ADMIN, "SeedAdmin__Password": ADMIN_PASSWORD})
    try:
        step("await page.setViewportSize({ width: 1440, height: 900 }); return JSON.stringify({});")
        url = h.config_url(slug, config)
        product_page = f"/Products/{slug}"

        # 1. A customer registers (shots of the forms), compares, buys twice.
        shot("05-dang-ky", "/Account/Register", ".account-card", highlight=True)
        step(r"""
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: 'Nguyễn Văn Khách', Phone: '0912345678', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  // The code page belongs to this browser's registration: shot here, at 1x.
  await page.addStyleTag({ content: '.cursor-ring, .cursor-dot { display: none !important; }' });
  await page.locator('.account-card').screenshot({ path: '__OTP__' });
  return JSON.stringify({});
""".replace("__E__", CUSTOMER).replace("__P__", PASSWORD).replace("__OTP__", str(OUT / "06-nhap-otp.png")))
        step(r"""
  await fill(page, { Code: '__C__' }); await submit(page);
  return JSON.stringify({});
""".replace("__C__", otp_for(log, CUSTOMER) or ""))
        step("await signOut(page); return JSON.stringify({});")
        shot("07-dang-nhap", "/Account/Login", ".account-card", highlight=True)
        sign_in(CUSTOMER, PASSWORD)
        r = step(r"""
  for (const s of ['__SLUG__', 'airpods-pro-3', 'macbook-pro-16-m5']) {
    await page.goto(APP + '/Products/' + s);
    if (await page.locator('.compare-add button').count()) await press(page, '.compare-add button');
  }
  await page.goto(APP + '__URL__');
  await press(page, '[data-choice-buy]');
  return JSON.stringify({});
""".replace("__SLUG__", slug).replace("__URL__", url))
        shot("08-so-sanh", "/Compare", ".account-card", highlight=True)
        shot("09-gio-hang", "/Cart", ".account-card", highlight=True)
        shot("10-thanh-toan", "/Checkout", ".account-card", before=r"""
    await p.fill('#AddressLine', '126 Nguyễn Thiện Thành');
    await p.fill('#VoucherCode', 'WELCOME10');
    await Promise.all([p.waitForNavigation(), p.click('button[value=apply]')]);
""", highlight=True)
        r = step(r"""
  await page.goto(APP + '/Checkout');
  await page.fill('#AddressLine', '126 Nguyễn Thiện Thành');
  await page.fill('#Ward', 'Phường 5').catch(() => {});
  await page.fill('#City', 'Trà Vinh').catch(() => {});
  await page.fill('#VoucherCode', 'WELCOME10');
  await press(page, 'button[value=apply]');
  await press(page, 'button[value=place]');
  const first = page.url().replace(APP, '');
  await page.goto(APP + '/Products/airpods-pro-3');
  await press(page, '.product-grid a.product-card >> nth=0');
  await press(page, '[data-choice-buy]');
  await page.goto(APP + '/Checkout');
  await page.fill('#AddressLine', '126 Nguyễn Thiện Thành');
  await press(page, 'button[value=place]');
  return JSON.stringify({ first });
""")
        order = r["first"].split("/")[-1]
        shot("11-don-hang", r["first"], ".account-card", highlight=True)
        shot("12-don-cua-toi", "/Orders", ".account-card", highlight=True)

        # 2. Staff deliver the first order, start a promotion, receive goods, sell at the counter.
        sign_in(ADMIN, ADMIN_PASSWORD)
        step(r"""
  await page.goto(APP + '/Admin/Orders/__ID__');
  await press(page, 'button[value=Confirm]');
  await page.fill('#Carrier', 'GHN'); await page.fill('#TrackingNo', 'GHN123456789');
  await press(page, 'button[value=Ship]');
  return JSON.stringify({});
""".replace("__ID__", order))
        shot("23-admin-chi-tiet-don", f"/Admin/Orders/{order}", ".account-card", highlight=True)
        r = step(r"""
  await page.goto(APP + '/Admin/Orders/__ID__');
  await press(page, 'button[value=Complete]');
  await page.goto(APP + '/Admin/Promotions/New');
  await page.fill('#Name', 'Ưu đãi tháng 10');
  await page.selectOption('#Type', 'Percent');
  await page.fill('#Value', '5');
  await press(page, 'button:has-text("Save promotion")');
  await page.goto(APP + '/Admin/Stock/New');
  await page.fill('#Supplier', 'Công ty FPT Trading');
  await page.selectOption('select[name="Lines[0].VariantId"]', '__A__');
  await page.fill('input[name="Lines[0].Quantity"]', '10');
  await page.fill('input[name="Lines[0].UnitCost"]', '21000000');
  await page.click('[data-add-line]');
  await page.selectOption('select[name="Lines[1].VariantId"]', '__B__');
  await page.fill('input[name="Lines[1].Quantity"]', '5');
  await page.fill('input[name="Lines[1].UnitCost"]', '500000');
  await press(page, 'button:has-text("Save receipt")');
  const receipt = page.url().replace(APP, '');
  await page.goto(APP + '/Admin/Sales/New');
  const saleForm = page.url().replace(APP, '');
  await page.selectOption('select[name="Lines[0].VariantId"]', '__A__');
  await page.fill('input[name="Lines[0].Quantity"]', '1');
  await page.click('[data-add-line]');
  await page.selectOption('select[name="Lines[1].VariantId"]', '__B__');
  await page.fill('input[name="Lines[1].Quantity"]', '2');
  await page.fill('#CustomerName', 'Trần Thị Lan');
  await press(page, 'button[value=quote]');
  await page.addStyleTag({ content: '.cursor-ring, .cursor-dot { display: none !important; }' });
  await page.locator('.account-card').screenshot({ path: '__QUOTE__' });
  await press(page, 'button[value=sell]');
  const sale = page.url().replace(APP, '');
  return JSON.stringify({ receipt, sale });
""".replace("__ID__", order).replace("__A__", str(va)).replace("__B__", str(vb)).replace("__QUOTE__", str(OUT / "28-ban-tai-quay.png")))
        sale = r["sale"].split("/")[-1]
        shot("25-nhap-kho", "/Admin/Stock/New", ".account-card", before=r"""
    await p.fill('#Supplier', 'Công ty FPT Trading');
    await p.selectOption('select[name="Lines[0].VariantId"]', '__A__');
    await p.fill('input[name="Lines[0].Quantity"]', '10');
    await p.fill('input[name="Lines[0].UnitCost"]', '21000000');
    await p.click('[data-add-line]');
""".replace("__A__", str(va)), highlight=True)
        shot("26-phieu-nhap", r["receipt"], ".account-card", highlight=True)
        shot("27-ton-kho", "/Admin/Stock/Levels", ".account-card", top=900, highlight=True)
        shot("29-don-tai-quay", r["sale"], ".account-card", highlight=True)
        shot("30-in-hoa-don-loat", f"/Admin/Orders/Invoices?ids={order}&ids={sale}", full=True)
        for name, path in [("14-admin-tong-quan", "/Admin"), ("15-admin-don-hang", "/Admin/Orders"), ("16-admin-san-pham", "/Admin/Products"),
                           ("17-admin-voucher", "/Admin/Vouchers"), ("18-admin-khuyen-mai", "/Admin/Promotions"),
                           ("20-admin-bao-cao", "/Admin/Reports"), ("22-admin-tai-khoan", "/Admin/Users")]:
            shot(name, path, ".account-card", top=900 if name == "16-admin-san-pham" else None, highlight=True)
        r = step(r"""
  await page.goto(APP + '/Admin/Prices');
  // One product with four variants, so the history stays short enough to read.
  await page.selectOption('#ProductIds', { label: 'Mac Mini' });
  await page.selectOption('#Mode', 'Percent');
  await page.fill('#Value', '2');
  await press(page, 'button:has-text("Change prices")');
  return JSON.stringify({ status: await text(page, '.cart-status'), error: await text(page, '.account-error') });
""")
        assert r["status"] and r["status"][0].startswith("Changed "), f"price batch: {r}"
        shot("19-admin-gia", "/Admin/Prices", ".account-card", highlight=True)

        # 3. The customer reviews what was delivered; staff see it.
        sign_in(CUSTOMER, PASSWORD)
        step(r"""
  await page.goto(APP + '__PRODUCT__');
  await page.selectOption('select[name=Rating]', '5');
  await page.fill('textarea[name=Content]', 'Máy đẹp, giao hàng nhanh, đóng gói cẩn thận.');
  await press(page, 'button:has-text("Post review")');
  return JSON.stringify({});
""".replace("__PRODUCT__", product_page))
        shot("24-danh-gia", product_page, ".pdp-reviews", highlight=True)
        sign_in(ADMIN, ADMIN_PASSWORD)
        shot("21-admin-danh-gia", "/Admin/Reviews", ".account-card", highlight=True)

        # 4. Visitor pages last, so they show the promotion and the review.
        step("await signOut(page); return JSON.stringify({});")
        shot("01-trang-chu", "/", wait=2500)
        shot("02-danh-muc", "/Products?category=iphone", ".shop-page", highlight=True)
        shot("03-tim-kiem-loc-gia", "/Products?q=iphone&band=Over40M&sort=PriceAscending")
        shot("04-cau-hinh-san-pham", url, ".product-detail", highlight=True)
        shot("13-tra-cuu-don", "/Track", ".account-card", before=r"""
    await p.fill('#OrderId', '__ID__');
    await p.fill('#Phone', '0912345678');
    await Promise.all([p.waitForNavigation(), p.click('main form button[type=submit]')]);
""".replace("__ID__", order), highlight=True)
        shot("31-anh-cho", "/Products?category=watch", ".model-strip", highlight=True)
    finally:
        h.stop_app(proc)
        h.close_browser()
    print("\n".join(sorted(p.name for p in OUT.glob("*.png"))))


if __name__ == "__main__":
    main()
