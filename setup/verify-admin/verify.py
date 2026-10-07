"""Live check of the Admin area and the seeded admin account in a real
browser, against the real app and throwaway databases.

Usage (from the repo root, playwright-cli installed, nothing on port 5287):
  python3 setup/verify-admin/verify.py

What it does:
  1. Builds a fresh database with "dotnet ef database update" (the real
     migrations, catalog included) and starts the app with SeedAdmin set
     through environment variables, the way a deployed copy would get them.
     The dev database and the owner's user-secrets are never used.
  2. Drives Chromium through the paths in each numbered block below: visitor,
     admin, customer, sign out, at 1440px and 390px.
  3. Restarts the app to check the seeder only ever creates once, and starts
     it on an unmigrated database and with a weak password to check both are
     logged and the app still serves pages.
  4. Prints one PASS/FAIL line per check and exits non-zero on any FAIL.
     Screenshots go to the temp folder, not the repo.
"""
import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from verifylib import Harness, migrate, otp_for, scalar  # noqa: E402

h = Harness("verify-admin", 5287)
APP, TMP = h.app, h.tmp
check, step = h.check, h.step
STAMP = str(int(time.time()))
ADMIN = f"admin{STAMP}@example.com"
ADMIN_PASSWORD = f"Admin-{STAMP}"
CUSTOMER = f"customer{STAMP}@example.com"
PASSWORD = "Password1"
SEED = {"SeedAdmin__Email": ADMIN, "SeedAdmin__Password": ADMIN_PASSWORD, "SeedAdmin__FullName": "Store Admin"}


def sign_in(email, password):
    return r"""
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__E__', Password: '__P__' }); await submit(page);
""".replace("__E__", email).replace("__P__", password)


def main():
    db = TMP / "app.db"
    log = TMP / "app.log"
    h.open_browser()
    migrate(db)
    check("ef database update with SeedAdmin set creates no user", scalar(db, "select count(*) from Users") == 0)

    proc = h.start_app("Development", db, log, SEED)
    try:
        # 1. The seeder ran once at startup.
        text = log.read_text()
        check("startup log says the admin was created", f"Admin account {ADMIN} created" in text)
        check("the password is not in the log", ADMIN_PASSWORD not in text)
        check("exactly one admin row", scalar(db, "select count(*) from Users where Role = 2") == 1)

        # 2. Visitor: /Admin asks to sign in, then comes back to /Admin.
        r = step(r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  await page.setViewportSize({ width: 1440, height: 900 });
  const out = {};
  await page.goto(APP + '/');
  out.anonLink = await page.locator('a[href="/Admin"]').count();
  await page.goto(APP + '/Admin');
  out.redirect = page.url().replace(APP, '');
  await fill(page, { Email: '__ADMIN__', Password: '__PW__' }); await submit(page);
  out.landed = page.url().replace(APP, '');
  out.title = await page.locator('h1').innerText();
  out.stats = Object.fromEntries(await page.locator('[data-stat]').evaluateAll(els => els.map(e => [e.dataset.stat, e.innerText.trim()])));
  out.adminNav = await text(page, '.admin-nav a');
  out.nav = (await page.locator('.site-nav-account').innerText()).replace(/\n/g, ' | ');
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""".replace("__ADMIN__", ADMIN).replace("__PW__", ADMIN_PASSWORD))
        check("a visitor sees no Admin link", r["anonLink"] == 0)
        check("a visitor on /Admin is sent to sign in", r["redirect"] == "/Account/Login?ReturnUrl=%2FAdmin", r["redirect"])
        check("signing in from there lands on the dashboard", r["landed"] == "/Admin" and r["title"] == "Dashboard", str(r))
        products = scalar(db, "select count(*) from Products")
        on_sale = scalar(db, "select count(*) from Products where Status = 1")
        check("dashboard counts match the database",
              r["stats"] == {"products": str(products), "products-on-sale": str(on_sale), "customers": "0", "orders": "0"},
              f"{r['stats']} vs products={products} on_sale={on_sale}")
        check("admin nav lists the dashboard and the way back", r["adminNav"] == ["Dashboard", "Back to the store"], str(r["adminNav"]))
        check("the site nav shows the cart, Admin, the name and Sign out", r["nav"] == "Cart | 0 | Admin | Store Admin | Sign out", r["nav"])
        check("no console errors so far", r["consoleErrors"] == [], str(r["consoleErrors"]))

        # 3. Layout at both widths, the nav link on a phone, the way back.
        r = step(r"""
  const out = {};
  for (const [w, hgt] of [[1440, 900], [390, 844]]) {
    await page.setViewportSize({ width: w, height: hgt });
    await page.goto(APP + '/Admin');
    out['scroll' + w] = await page.evaluate(() => document.documentElement.scrollWidth);
    out['statsVisible' + w] = await page.locator('.admin-stat').evaluateAll(els => els.every(e => e.getBoundingClientRect().right <= window.innerWidth));
    await page.screenshot({ path: 'dashboard-' + w + '.png', fullPage: true });
  }
  await page.goto(APP + '/');
  await page.click('.navbar-toggler');
  await page.locator('a[href="/Admin"]').waitFor({ state: 'visible' });
  await page.click('a[href="/Admin"]');
  await page.waitForLoadState('load');
  out.phoneLink = page.url().replace(APP, '');
  await page.click('.admin-nav >> text=Back to the store');
  await page.waitForLoadState('load');
  out.back = page.url().replace(APP, '');
  await page.setViewportSize({ width: 1440, height: 900 });
  return JSON.stringify(out);
""")
        check("no sideways scroll at 1440px", r["scroll1440"] <= 1440, str(r["scroll1440"]))
        check("no sideways scroll at 390px", r["scroll390"] <= 390, str(r["scroll390"]))
        check("every stat card fits the screen at both widths", r["statsVisible1440"] and r["statsVisible390"])
        check("the Admin link works from the phone menu", r["phoneLink"] == "/Admin", r["phoneLink"])
        check("Back to the store goes home", r["back"] == "/", r["back"])

        # 4. Sign out, then /Admin asks to sign in again.
        r = step(r"""
  await page.goto(APP + '/Admin');
  await signOut(page);
  await page.goto(APP + '/Admin');
  return JSON.stringify({ url: page.url().replace(APP, '') });
""")
        check("after signing out /Admin asks to sign in", r["url"].startswith("/Account/Login"), r["url"])

        # 5. A customer (registered through the real form) is turned away.
        step(r"""
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__C__', FullName: 'Plain Customer', Phone: '', Password: '__P__', ConfirmPassword: '__P__' });
  await submit(page);
  return JSON.stringify({});
""".replace("__C__", CUSTOMER).replace("__P__", PASSWORD))
        code = otp_for(log, CUSTOMER)
        r = step(r"""
  const out = {};
  await fill(page, { Code: '__CODE__' }); await submit(page);
  out.signedIn = page.url().replace(APP, '');
  out.link = await page.locator('a[href="/Admin"]').count();
  for (const p of ['/Admin', '/Admin/Dashboard/Index']) {
    await page.goto(APP + p);
    out[p] = page.url().replace(APP, '');
  }
  out.heading = await page.locator('h1').innerText();
  await page.click('main >> text=Back to the store');
  await page.waitForLoadState('load');
  out.back = page.url().replace(APP, '');
  await page.screenshot({ path: 'customer-denied.png' });
  await signOut(page);
  return JSON.stringify(out);
""".replace("__CODE__", code or ""))
        check("the customer registered and signed in", r["signedIn"] == "/Account", r["signedIn"])
        check("a customer sees no Admin link", r["link"] == 0)
        check("a customer on /Admin gets access denied",
              r["/Admin"].startswith("/Account/AccessDenied") and r["/Admin/Dashboard/Index"].startswith("/Account/AccessDenied"), str(r))
        check("the access denied page explains and links home",
              r["heading"] == "You do not have access to this page" and r["back"] == "/", str(r))

        # 6. The dashboard sees the new customer.
        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  await page.goto(APP + '/Admin');
  const out = { customers: await page.locator('[data-stat="customers"]').innerText() };
  await signOut(page);
  return JSON.stringify(out);
""")
        check("the customer count goes up after a registration", r["customers"] == "1", r["customers"])
    finally:
        h.stop_app(proc)

    # 7. Restart with a different password: nothing changes.
    log2 = TMP / "app-restart.log"
    proc = h.start_app("Development", db, log2, dict(SEED, SeedAdmin__Password="Other-Pass-99"))
    try:
        r = step(sign_in(ADMIN, ADMIN_PASSWORD) + r"""
  const out = { url: page.url().replace(APP, '') };
  await signOut(page);
  return JSON.stringify(out);
""")
        text = log2.read_text()
        check("a restart with an admin present logs nothing from the seeder", "AdminSeeder" not in text)
        check("the first password still signs the admin in", r["url"] == "/", r["url"])
        check("still exactly one admin", scalar(db, "select count(*) from Users where Role = 2") == 1)
    finally:
        h.stop_app(proc)

    # 8. Weak password on a fresh database: logged, nothing created, app serves.
    weak_db, weak_log = TMP / "weak.db", TMP / "app-weak.log"
    migrate(weak_db)
    proc = h.start_app("Development", weak_db, weak_log, dict(SEED, SeedAdmin__Password="q7Z"))
    try:
        r = step("await page.goto(APP + '/'); return JSON.stringify({ title: await page.title() });")
        text = weak_log.read_text()
        check("a weak password is logged with the reason", "was not created" in text and "at least 8" in text, "")
        check("the weak password itself is not logged", "q7Z" not in text)
        check("no admin is created", scalar(weak_db, "select count(*) from Users") == 0)
        check("the store still serves pages", r["title"] != "", r["title"])
    finally:
        h.stop_app(proc)

    # 9. Database never migrated: logged as an error, app still answers.
    empty_db, empty_log = TMP / "empty.db", TMP / "app-empty.log"
    proc = h.start_app("Production", empty_db, empty_log, SEED)
    try:
        r = step("const resp = await page.goto(APP + '/Account/Login'); return JSON.stringify({ status: resp.status() });")
        text = empty_log.read_text()
        check("an unmigrated database is logged with what to run", "dotnet ef database update" in text, "")
        check("the app still answers", r["status"] == 200, json.dumps(r))
    finally:
        h.stop_app(proc)
        h.close_browser()

    h.finish()


if __name__ == "__main__":
    main()
