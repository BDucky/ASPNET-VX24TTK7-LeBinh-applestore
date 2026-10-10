"""Screenshots of the main pages for the report and slides (task 11), taken
from the real app on a fresh database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5300):
  python3 setup/screenshots/capture.py

The script plays a customer and the admin first (orders, a delivered order, a
promotion, a review, a stock receipt, a counter sale), then takes each shot
in a fresh 2x browser context with the same sign-in, cropped to the block
that matters (a card, the product panel, a table), so text stays readable
when the image is enlarged on a slide or printed in the report (owner agreed
2026-10-09 that whole-page shots were too small). Writes PNG files to
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


def shot(name, path, selector=None, before="", wait=1500, full=False):
    """A 2x shot of `path` with the current sign-in; cropped to `selector`."""
    target = f"await p.locator('{selector}').first().screenshot({{ path: '{OUT / (name + '.png')}' }});" if selector \
        else f"await p.screenshot({{ path: '{OUT / (name + '.png')}', fullPage: {'true' if full else 'false'} }});"
    step(r"""
  const ctx = await page.context().browser().newContext({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 2, storageState: await page.context().storageState() });
  const p = await ctx.newPage();
  try {
    await p.goto(APP + '__PATH__');
    __BEFORE__
    __HIDE__
    await p.waitForTimeout(__WAIT__);
    __TARGET__
  } finally {
    await ctx.close();
  }
  return JSON.stringify({});
""".replace("__PATH__", path).replace("__BEFORE__", before).replace("__HIDE__", HIDE)
        .replace("__WAIT__", str(wait)).replace("__TARGET__", target))


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
        shot("05-dang-ky", "/Account/Register", ".account-card")
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
        shot("07-dang-nhap", "/Account/Login", ".account-card")
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
        shot("08-so-sanh", "/Compare", ".account-card")
        shot("09-gio-hang", "/Cart", ".account-card")
        shot("10-thanh-toan", "/Checkout", ".account-card", before=r"""
    await p.fill('#AddressLine', '126 Nguyễn Thiện Thành');
    await p.fill('#VoucherCode', 'WELCOME10');
    await Promise.all([p.waitForNavigation(), p.click('button[value=apply]')]);
""")
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
        shot("11-don-hang", r["first"], ".account-card")
        shot("12-don-cua-toi", "/Orders", ".account-card")

        # 2. Staff deliver the first order, start a promotion, receive goods, sell at the counter.
        sign_in(ADMIN, ADMIN_PASSWORD)
        step(r"""
  await page.goto(APP + '/Admin/Orders/__ID__');
  await press(page, 'button[value=Confirm]');
  await page.fill('#Carrier', 'GHN'); await page.fill('#TrackingNo', 'GHN123456789');
  await press(page, 'button[value=Ship]');
  return JSON.stringify({});
""".replace("__ID__", order))
        shot("23-admin-chi-tiet-don", f"/Admin/Orders/{order}", ".account-card")
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
""".replace("__A__", str(va)))
        shot("26-phieu-nhap", r["receipt"], ".account-card")
        shot("27-ton-kho", "/Admin/Stock/Levels", ".account-card")
        shot("29-don-tai-quay", r["sale"], ".account-card")
        shot("30-in-hoa-don-loat", f"/Admin/Orders/Invoices?ids={order}&ids={sale}", full=True)
        for name, path in [("14-admin-tong-quan", "/Admin"), ("15-admin-don-hang", "/Admin/Orders"), ("16-admin-san-pham", "/Admin/Products"),
                           ("17-admin-voucher", "/Admin/Vouchers"), ("18-admin-khuyen-mai", "/Admin/Promotions"),
                           ("20-admin-bao-cao", "/Admin/Reports"), ("22-admin-tai-khoan", "/Admin/Users")]:
            shot(name, path, ".account-card")
        step(r"""
  await page.goto(APP + '/Admin/Prices');
  await page.selectOption('#ProductIds', { index: 1 });
  await page.selectOption('#Mode', 'Percent');
  await page.fill('#Value', '2');
  await press(page, 'button:has-text("Change prices")');
  return JSON.stringify({});
""")
        shot("19-admin-gia", "/Admin/Prices", ".account-card")

        # 3. The customer reviews what was delivered; staff see it.
        sign_in(CUSTOMER, PASSWORD)
        step(r"""
  await page.goto(APP + '__PRODUCT__');
  await page.selectOption('select[name=Rating]', '5');
  await page.fill('textarea[name=Content]', 'Máy đẹp, giao hàng nhanh, đóng gói cẩn thận.');
  await press(page, 'button:has-text("Post review")');
  return JSON.stringify({});
""".replace("__PRODUCT__", product_page))
        shot("24-danh-gia", product_page, ".pdp-reviews")
        sign_in(ADMIN, ADMIN_PASSWORD)
        shot("21-admin-danh-gia", "/Admin/Reviews", ".account-card")

        # 4. Visitor pages last, so they show the promotion and the review.
        step("await signOut(page); return JSON.stringify({});")
        shot("01-trang-chu", "/", wait=2500)
        shot("02-danh-muc", "/Products?category=iphone", ".shop-page")
        shot("03-tim-kiem-loc-gia", "/Products?q=iphone&band=Over40M&sort=PriceAscending")
        shot("04-cau-hinh-san-pham", url, ".product-detail")
        shot("13-tra-cuu-don", "/Track", ".account-card", before=r"""
    await p.fill('#OrderId', '__ID__');
    await p.fill('#Phone', '0912345678');
    await Promise.all([p.waitForNavigation(), p.click('main form button[type=submit]')]);
""".replace("__ID__", order))
        shot("31-anh-cho", "/Products?category=watch", ".model-strip")
    finally:
        h.stop_app(proc)
        h.close_browser()
    print("\n".join(sorted(p.name for p in OUT.glob("*.png"))))


if __name__ == "__main__":
    main()
