"""Screenshots of the main pages for the report and slides (task 11), taken
from the real app on a fresh database built by the real migrations.

Usage (from the repo root, playwright-cli installed, nothing on port 5300):
  python3 setup/screenshots/capture.py

Writes PNG files to thesis/doc/hinh/. Re-run after a UI change so the report
shows what the app really looks like.
"""
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import ROOT, Harness, migrate, otp_for, rows  # noqa: E402

h = Harness("screenshots", 5300)
APP, TMP = h.app, h.tmp
# Every step may hide the cursor before a screenshot with __HIDE__.
def step(body):
    return h.step(body.replace("__HIDE__", HIDE))


OUT = ROOT / "thesis" / "doc" / "hinh"
STAMP = str(int(time.time()))
ADMIN, ADMIN_PASSWORD = f"admin{STAMP}@example.com", f"Admin-{STAMP}"
CUSTOMER, PASSWORD = f"khach{STAMP}@example.com", "Password1"


# The site's custom cursor ring would show in every shot.
HIDE = "  await page.addStyleTag({ content: '.cursor-ring, .cursor-dot { display: none !important; }' });\n"


def shot(name, path, full=False, wait=1500, before=""):
    step(r"""
  await page.goto(APP + '__PATH__');
  __BEFORE__
  await page.waitForTimeout(__WAIT__);
__HIDE__
  await page.screenshot({ path: '__OUT__', fullPage: __FULL__ });
  return JSON.stringify({});
""".replace("__PATH__", path).replace("__BEFORE__", before).replace("__WAIT__", str(wait))
        .replace("__OUT__", str(OUT / f"{name}.png")).replace("__FULL__", "true" if full else "false"))


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
    proc = h.start_app("Development", db, log, {"SeedAdmin__Email": ADMIN, "SeedAdmin__Password": ADMIN_PASSWORD})
    try:
        step("await page.setViewportSize({ width: 1440, height: 900 }); return JSON.stringify({});")
        url = h.config_url(slug, config)
        product_page = f"/Products/{slug}"

        # A customer registers, compares, buys two orders.
        shot("05-dang-ky", "/Account/Register")
        step(r"""
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__E__', FullName: 'Nguyễn Văn Khách', Phone: '0912345678', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
__HIDE__
  await page.screenshot({ path: '__OUT__' });
  return JSON.stringify({});
""".replace("__E__", CUSTOMER).replace("__P__", PASSWORD).replace("__OUT__", str(OUT / "06-nhap-otp.png")))
        step(r"""
  await fill(page, { Code: '__C__' }); await submit(page);
  return JSON.stringify({});
""".replace("__C__", otp_for(log, CUSTOMER) or ""))
        shot("07-dang-nhap", "/Account/Login", before="await signOut(page); await page.goto(APP + '/Account/Login');")
        sign_in(CUSTOMER, PASSWORD)
        r = step(r"""
  for (const s of ['__SLUG__', 'airpods-pro-3', 'macbook-pro-16-m5']) {
    await page.goto(APP + '/Products/' + s);
    if (await page.locator('.compare-add button').count()) await press(page, '.compare-add button');
  }
  await page.goto(APP + '__URL__');
  await press(page, '[data-choice-buy]');
  await page.goto(APP + '/Checkout');
  await page.fill('#AddressLine', '126 Nguyễn Thiện Thành');
  await page.fill('#Ward', 'Phường 5').catch(() => {});
  await page.fill('#City', 'Trà Vinh').catch(() => {});
  await page.fill('#VoucherCode', 'WELCOME10');
  await press(page, 'button[value=apply]');
__HIDE__
  await page.screenshot({ path: '__CHECKOUT__', fullPage: true });
  await press(page, 'button[value=place]');
  const first = page.url().replace(APP, '');
__HIDE__
  await page.screenshot({ path: '__ORDER__', fullPage: true });
  await page.goto(APP + '/Products/airpods-pro-3');
  const config = page.locator('.product-grid a.product-card').first();
  await press(page, '.product-grid a.product-card >> nth=0');
  await press(page, '[data-choice-buy]');
  await page.goto(APP + '/Checkout');
  await page.fill('#AddressLine', '126 Nguyễn Thiện Thành');
  await press(page, 'button[value=place]');
  return JSON.stringify({ first });
""".replace("__SLUG__", slug).replace("__URL__", url).replace("__CHECKOUT__", str(OUT / "10-thanh-toan.png")).replace("__ORDER__", str(OUT / "11-don-hang.png")))
        order = r["first"].split("/")[-1]
        shot("08-so-sanh", "/Compare", full=True)
        shot("12-don-cua-toi", "/Orders")

        # Staff deliver the first order (cash, so it counts as paid) and start a promotion.
        sign_in(ADMIN, ADMIN_PASSWORD)
        step(r"""
  await page.goto(APP + '/Admin/Orders/__ID__');
  await press(page, 'button[value=Confirm]');
  await page.fill('#Carrier', 'GHN'); await page.fill('#TrackingNo', 'GHN123456789');
  await press(page, 'button[value=Ship]');
__HIDE__
  await page.screenshot({ path: '__DETAIL__', fullPage: true });
  await press(page, 'button[value=Complete]');
  await page.goto(APP + '/Admin/Promotions/New');
  await page.fill('#Name', 'Ưu đãi tháng 10');
  await page.selectOption('#Type', 'Percent');
  await page.fill('#Value', '5');
  await press(page, 'button:has-text("Save promotion")');
  return JSON.stringify({});
""".replace("__ID__", order).replace("__DETAIL__", str(OUT / "23-admin-chi-tiet-don.png")))
        for name, path in [("14-admin-tong-quan", "/Admin"), ("15-admin-don-hang", "/Admin/Orders"), ("16-admin-san-pham", "/Admin/Products"),
                           ("17-admin-voucher", "/Admin/Vouchers"), ("18-admin-khuyen-mai", "/Admin/Promotions"), ("19-admin-gia", "/Admin/Prices"),
                           ("20-admin-bao-cao", "/Admin/Reports"), ("22-admin-tai-khoan", "/Admin/Users")]:
            shot(name, path)

        # The customer reviews what was delivered; staff see it.
        sign_in(CUSTOMER, PASSWORD)
        step(r"""
  await page.goto(APP + '__PRODUCT__');
  await page.selectOption('select[name=Rating]', '5');
  await page.fill('textarea[name=Content]', 'Máy đẹp, giao hàng nhanh, đóng gói cẩn thận.');
  await press(page, 'button:has-text("Post review")');
  await page.locator('.pdp-reviews').scrollIntoViewIfNeeded();
__HIDE__
  await page.locator('.pdp-reviews').screenshot({ path: '__OUT__' });
  return JSON.stringify({});
""".replace("__PRODUCT__", product_page).replace("__OUT__", str(OUT / "24-danh-gia.png")))
        sign_in(ADMIN, ADMIN_PASSWORD)
        shot("21-admin-danh-gia", "/Admin/Reviews")

        # Visitor pages last, so they show the promotion and the review.
        step("await signOut(page); return JSON.stringify({});")
        shot("01-trang-chu", "/", wait=2500)
        shot("02-danh-muc", "/Products?category=iphone")
        shot("03-tim-kiem-loc-gia", "/Products?q=iphone&band=Over40M&sort=PriceAscending")
        shot("04-cau-hinh-san-pham", url)
        shot("13-tra-cuu-don", "/Track", before=r"""
  await page.fill('#OrderId', '__ID__').catch(() => {});
  await page.fill('#Phone', '0912345678').catch(() => {});
  if (await page.locator('main form button[type=submit]').count()) await press(page, 'main form button[type=submit]');
""".replace("__ID__", order))
        shot("09-gio-hang", "/Cart", before="")
    finally:
        h.stop_app(proc)
        h.close_browser()
    print("\n".join(sorted(p.name for p in OUT.glob("*.png"))))


if __name__ == "__main__":
    main()
