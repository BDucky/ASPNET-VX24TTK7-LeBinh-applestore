"""Live check of the account pages (register, verify code, sign in, sign out,
forgot and change password, profile, delivery addresses, lockout) in a real
browser, against the real app and a throwaway copy of the
database.

Usage (from the repo root, playwright-cli installed, nothing on port 5286):
  python3 setup/verify-account/verify.py

What it does:
  1. Copies src/AppleStore.Web/AppleStore.db to a temp folder and starts the
     app on http://localhost:5286 against that copy, logging to a file. The
     dev database is never written to.
  2. Drives Chromium through every account page and path listed in CHECKS
     below, reading each OTP from the app log (DevEmailSender logs mail
     instead of sending it).
  3. Restarts the app in Production mode against an empty database to check
     that a server error still ends on a page with a way back.
  4. Prints one PASS/FAIL line per check and exits non-zero on any FAIL.
     Screenshots go to the temp folder, not the repo.
"""
import json
import os
import re
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WEB = ROOT / "src" / "AppleStore.Web"
APP = "http://localhost:5286"
TMP = Path(tempfile.mkdtemp(prefix="verify-account-"))
STAMP = str(int(time.time()))
EMAIL = f"verify{STAMP}@example.com"
PHONE = "09" + STAMP[-8:]
PASSWORD = "Password1"
SESSION = "verify-account"
results = []


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(f"{'PASS' if ok else 'FAIL'}  {name}" + (f"  ({detail})" if detail else ""))


def start_app(env_name, db_path, log_path, smtp=None):
    # Smtp__Host empty keeps mail in the log even when user-secrets hold real
    # SMTP settings, so the script can read each code and never mails anyone.
    env = dict(os.environ, ASPNETCORE_ENVIRONMENT=env_name, ConnectionStrings__Default=f"Data Source={db_path}", Smtp__Host="")
    env.update(smtp or {})
    log = open(log_path, "w")
    proc = subprocess.Popen(
        ["dotnet", "run", "--no-build", "--no-launch-profile", "--urls", APP],
        cwd=WEB, env=env, stdout=log, stderr=subprocess.STDOUT)
    for _ in range(60):
        try:
            urllib.request.urlopen(APP + "/Account/Login", timeout=2)
            return proc
        except urllib.error.HTTPError:
            return proc
        except Exception:
            time.sleep(1)
    proc.kill()
    sys.exit("app did not start, see " + str(log_path))


def stop_app(proc):
    proc.terminate()
    try:
        proc.wait(timeout=15)
    except subprocess.TimeoutExpired:
        proc.kill()


def run(js):
    path = TMP / "step.js"
    path.write_text(js)
    out = subprocess.run(["playwright-cli", "-s=" + SESSION, "--raw", "run-code", "--filename=" + str(path)],
                         capture_output=True, text=True, cwd=TMP)
    text = out.stdout.strip()
    try:
        value = json.loads(text)
        # Steps return JSON.stringify(...), which --raw prints as a JSON string.
        return json.loads(value) if isinstance(value, str) else value
    except json.JSONDecodeError:
        sys.exit("browser step failed:\n" + text + out.stderr)


def otp_for(log_path, email):
    text = Path(log_path).read_text()
    codes = re.findall(re.escape(email) + r"[^\n]*\n[^\n]*?Your code is (\d{6})", text, re.IGNORECASE)
    return codes[-1] if codes else None


# Shared browser helpers, prepended to every step.
LIB = r"""
const APP = '__APP__';
const text = async (page, sel) => (await page.locator(sel).allTextContents()).map(s => s.trim()).filter(Boolean);
const errors = page => text(page, '.account-error');
const fieldErrors = page => text(page, '.account-field-error');
const submit = async page => { await page.click('.account-form button[type=submit]'); await page.waitForLoadState('load'); };
const fill = async (page, fields) => { for (const [k, v] of Object.entries(fields)) await page.fill('.account-form #' + k, v); };
const signOut = async page => {
  if (await page.locator('.site-nav-account-out').count()) { await page.click('.site-nav-account-out'); await page.waitForLoadState('load'); }
};
"""


def step(body):
    return run("async page => {\n" + LIB.replace("__APP__", APP) + body + "\n}")


def main():
    shutil.copy(WEB / "AppleStore.db", TMP / "app.db")
    log = TMP / "app.log"
    subprocess.run(["dotnet", "build", "-v", "q", str(WEB)], check=True, capture_output=True)
    subprocess.run(["playwright-cli", "-s=" + SESSION, "open"], capture_output=True, cwd=TMP)
    proc = start_app("Development", TMP / "app.db", log)
    try:
        # 1. Every account page loads, has no console errors, no sideways scroll.
        r = step(r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  const out = {};
  for (const [w, h] of [[1440, 900], [390, 844]]) {
    await page.setViewportSize({ width: w, height: h });
    for (const p of ['/Account/Login', '/Account/Register', '/Account/AccessDenied']) {
      const resp = await page.goto(APP + p);
      out[p + '@' + w] = { status: resp.status(), scroll: await page.evaluate(() => document.documentElement.scrollWidth) };
    }
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  const acc = await page.goto(APP + '/Account');
  out.accountRedirect = page.url().replace(APP, '');
  await page.goto(APP + '/Account/VerifyOtp');
  out.verifyRedirect = page.url().replace(APP, '');
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""")
        for key, v in r.items():
            if "@" in key:
                width = int(key.split("@")[1])
                check(f"page {key} loads without sideways scroll", v["status"] == 200 and v["scroll"] <= width, json.dumps(v))
        check("anonymous /Account goes to sign in", r["accountRedirect"] == "/Account/Login?ReturnUrl=%2FAccount", r["accountRedirect"])
        check("/Account/VerifyOtp with no registration goes to register", r["verifyRedirect"] == "/Account/Register", r["verifyRedirect"])
        check("no console errors on account pages", not r["consoleErrors"], "; ".join(r["consoleErrors"]))

        # 2. Register: client-side validation, password rule, then a valid form.
        r = step(r"""
  const posts = [];
  page.on('request', q => { if (q.method() === 'POST') posts.push(q.url()); });
  await page.goto(APP + '/Account/Register');
  await page.click('.account-form button[type=submit]');
  await page.waitForTimeout(300);
  const out = { emptyErrors: await fieldErrors(page), emptyPosts: posts.length };
  await fill(page, { Email: '__EMAIL__', FullName: 'Verify User', Phone: '__PHONE__', Password: 'Password1', ConfirmPassword: 'Password2' });
  await page.click('.account-form button[type=submit]');
  await page.waitForTimeout(300);
  out.mismatch = await fieldErrors(page);
  out.mismatchPosts = posts.length;
  await fill(page, { Password: 'short1', ConfirmPassword: 'short1' });
  await submit(page);
  out.short = await fieldErrors(page);
  await fill(page, { Password: '__PASSWORD__', ConfirmPassword: '__PASSWORD__' });
  await submit(page);
  out.url = page.url().replace(APP, '');
  await page.screenshot({ path: 'verify-otp.png' });
  return JSON.stringify(out);
""".replace("__EMAIL__", EMAIL).replace("__PHONE__", PHONE).replace("__PASSWORD__", PASSWORD))
        check("empty register form is stopped in the browser", r["emptyPosts"] == 0 and len(r["emptyErrors"]) >= 3, str(r["emptyErrors"]))
        check("mismatched passwords are stopped in the browser", r["mismatchPosts"] == 0 and "The two passwords do not match." in r["mismatch"], str(r["mismatch"]))
        check("short password shows the 8-character rule", "Password must be at least 8 characters." in r["short"], str(r["short"]))
        check("valid form goes to the code page", r["url"].startswith("/Account/VerifyOtp?attemptId="), r["url"])
        verify_url = r["url"]

        code = otp_for(log, EMAIL)
        check("OTP email was produced", code is not None)

        # 3. Code page: wrong code, right code, then the same code again.
        r = step(r"""
  const out = {};
  await fill(page, { Code: '000000' }); await submit(page);
  out.wrong = await errors(page);
  await fill(page, { Code: '__CODE__' }); await submit(page);
  out.after = page.url().replace(APP, '');
  out.nav = (await page.locator('.site-nav-account').innerText()).replace(/\n/g, ' | ');
  out.details = (await page.locator('.account-details').innerText()).replace(/\n/g, ' | ');
  await page.screenshot({ path: 'account.png' });
  await page.goto(APP + '__VERIFY__');
  await fill(page, { Code: '__CODE__' }); await submit(page);
  out.reuse = await errors(page);
  out.reuseLinks = await text(page, '.account-switch a');
  return JSON.stringify(out);
""".replace("__CODE__", code or "").replace("__VERIFY__", verify_url))
        check("wrong code is refused", r["wrong"] == ["The code is incorrect or has expired."], str(r["wrong"]))
        check("right code signs the new user in", r["after"] == "/Account" and r["nav"] == "Verify User | Sign out", f"{r['after']} {r['nav']}")
        check("account page shows the new user", EMAIL in r["details"] and PHONE in r["details"] and "Customer" in r["details"], r["details"])
        check("reusing a spent code says expired and offers a way out",
              r["reuse"] == ["This registration has expired. Please register again."] and r["reuseLinks"] == ["Register again", "sign in"], str(r))

        # 4. Sign out (GET does nothing, POST without token is refused, POST signs out).
        r = step(r"""
  const out = {};
  out.getStatus = (await page.request.get(APP + '/Account/Logout')).status();
  await page.goto(APP + '/Account');
  out.stillIn = page.url() === APP + '/Account';
  out.noTokenStatus = (await page.request.post(APP + '/Account/Logout', { form: {} })).status();
  await page.goto(APP + '/Account');
  out.stillInAfterNoToken = page.url() === APP + '/Account';
  await signOut(page);
  await page.goto(APP + '/Account');
  out.afterSignOut = page.url().replace(APP, '');
  return JSON.stringify(out);
""")
        check("GET /Account/Logout does not sign out", r["getStatus"] == 404 and r["stillIn"], str(r))
        check("POST /Account/Logout without anti-forgery token is refused", r["noTokenStatus"] == 400 and r["stillInAfterNoToken"], str(r))
        check("sign out button signs out", r["afterSignOut"].startswith("/Account/Login"), r["afterSignOut"])

        # 5. Duplicates: same email in other case, same phone.
        r = step(r"""
  const out = {};
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: '__UPPER__', FullName: 'Dup', Phone: '', Password: 'Password1', ConfirmPassword: 'Password1' });
  await submit(page);
  out.email = await fieldErrors(page);
  await fill(page, { Email: 'other__EMAIL__', Phone: '__PHONE__', Password: 'Password1', ConfirmPassword: 'Password1' });
  await submit(page);
  out.phone = await fieldErrors(page);
  return JSON.stringify(out);
""".replace("__UPPER__", EMAIL.upper()).replace("__EMAIL__", EMAIL).replace("__PHONE__", PHONE))
        check("same email in other case is refused", r["email"] == ["An account with this email already exists."], str(r["email"]))
        check("same phone is refused", r["phone"] == ["This phone number is already used by another account."], str(r["phone"]))

        # 6. Sign in: same message for both failures, return URLs, remember me.
        r = step(r"""
  const out = {};
  const login = async (email, pw, returnUrl, remember) => {
    await page.goto(APP + '/Account/Login' + (returnUrl ? '?returnUrl=' + encodeURIComponent(returnUrl) : ''));
    await fill(page, { Email: email, Password: pw });
    if (remember) await page.check('.account-form #RememberMe');
    await submit(page);
  };
  const cookie = async () => (await page.context().cookies()).find(c => c.name === '.AspNetCore.Identity.Application');
  await login('__EMAIL__', 'WrongPass1'); out.wrongPw = await errors(page);
  await login('nobody__STAMP__@example.com', 'Password1'); out.unknown = await errors(page);
  await login('__UPPER__', '__PASSWORD__', '/Account'); out.local = page.url().replace(APP, '');
  out.sessionCookie = (await cookie())?.expires;
  await signOut(page);
  await login('__EMAIL__', '__PASSWORD__', 'https://evil.example/', true); out.external = page.url();
  out.rememberCookie = (await cookie())?.expires;
  await signOut(page);
  await page.screenshot({ path: 'login.png' });
  return JSON.stringify(out);
""".replace("__EMAIL__", EMAIL).replace("__UPPER__", EMAIL.upper()).replace("__STAMP__", STAMP).replace("__PASSWORD__", PASSWORD))
        check("wrong password and unknown email give the same message",
              r["wrongPw"] == r["unknown"] == ["Email or password is incorrect."], f"{r['wrongPw']} / {r['unknown']}")
        check("sign in with upper-case email follows a local return URL", r["local"] == "/Account", r["local"])
        check("an external return URL lands on the home page", r["external"] == APP + "/", r["external"])
        check("without remember me the cookie ends with the browser", r["sessionCookie"] == -1, str(r["sessionCookie"]))
        check("remember me makes the cookie persistent", (r["rememberCookie"] or 0) > time.time() + 86400, str(r["rememberCookie"]))

        # 7. Forgot password: unknown email, wrong code, right code.
        r = step(r"""
  const out = {};
  await page.goto(APP + '/Account/Login');
  out.forgotLink = await page.locator('a[href="/Account/ForgotPassword"]').count();
  await page.goto(APP + '/Account/ForgotPassword');
  await fill(page, { Email: 'nobody__STAMP__@example.com' }); await submit(page);
  out.unknown = await fieldErrors(page);
  out.registerLink = await page.locator('.account-switch a[href="/Account/Register"]').count();
  await fill(page, { Email: '__EMAIL__' }); await submit(page);
  out.url = page.url().replace(APP, '');
  return JSON.stringify(out);
""".replace("__EMAIL__", EMAIL).replace("__STAMP__", STAMP))
        check("login page links to forgot password", r["forgotLink"] == 1, str(r["forgotLink"]))
        check("forgot password with an unknown email says so and links to register",
              r["unknown"] == ["No account uses this email."] and r["registerLink"] == 1, str(r))
        check("forgot password with a known email goes to the reset page", r["url"].startswith("/Account/ResetPassword?email="), r["url"])
        reset_code = otp_for(log, EMAIL)
        check("reset code email was produced", reset_code is not None and reset_code != code)
        new_password = "Password2"
        r = step(r"""
  const out = {};
  await fill(page, { Code: '__WRONG__', NewPassword: '__NEW__', ConfirmPassword: '__NEW__' }); await submit(page);
  out.wrong = await errors(page);
  await fill(page, { Code: '__CODE__', NewPassword: '__NEW__', ConfirmPassword: '__NEW__' }); await submit(page);
  out.url = page.url().replace(APP, '');
  out.status = await text(page, '.account-status');
  await page.screenshot({ path: 'reset-done.png' });
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__EMAIL__', Password: '__OLD__' }); await submit(page);
  out.oldPassword = await errors(page);
  await fill(page, { Email: '__EMAIL__', Password: '__NEW__' }); await submit(page);
  out.newPassword = page.url().replace(APP, '');
  return JSON.stringify(out);
""".replace("__WRONG__", "000000" if reset_code != "000000" else "111111").replace("__CODE__", reset_code or "")
     .replace("__NEW__", new_password).replace("__OLD__", PASSWORD).replace("__EMAIL__", EMAIL))
        check("wrong reset code stays on the page", r["wrong"] == ["The code is incorrect or has expired."], str(r["wrong"]))
        check("right reset code signs in with a confirmation", r["url"] == "/Account" and r["status"] == ["Your password was reset."], str(r))
        check("after reset the old password fails and the new one works",
              r["oldPassword"] == ["Email or password is incorrect."] and r["newPassword"] == "/", str(r))

        # 8. Change password (signed in from step 7).
        changed_password = "Password3"
        r = step(r"""
  const out = {};
  await page.goto(APP + '/Account/ChangePassword');
  await fill(page, { CurrentPassword: 'WrongPass9', NewPassword: '__CHANGED__', ConfirmPassword: '__CHANGED__' }); await submit(page);
  out.wrong = await fieldErrors(page);
  await fill(page, { CurrentPassword: '__CURRENT__', NewPassword: '__CHANGED__', ConfirmPassword: '__CHANGED__' }); await submit(page);
  out.url = page.url().replace(APP, '');
  out.status = await text(page, '.account-status');
  await signOut(page);
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__EMAIL__', Password: '__CHANGED__' }); await submit(page);
  out.relogin = page.url().replace(APP, '');
  return JSON.stringify(out);
""".replace("__CHANGED__", changed_password).replace("__CURRENT__", new_password).replace("__EMAIL__", EMAIL))
        check("change password refuses a wrong current password", r["wrong"] == ["Current password is incorrect."], str(r["wrong"]))
        check("change password keeps the session with a confirmation",
              r["url"] == "/Account" and r["status"] == ["Your password was changed."], str(r))
        check("the changed password signs in", r["relogin"] == "/", r["relogin"])

        # 9. Profile: a phone another account uses, then a real change.
        db = sqlite3.connect(TMP / "app.db")
        other_phone = "08" + STAMP[-8:]
        db.execute("insert into Users (Email, NormalizedEmail, PasswordHash, FullName, Phone, Role, CreatedAt, UpdatedAt, SecurityStamp, AccessFailedCount)"
                   " values (?, ?, 'x', 'Other', ?, 0, datetime('now'), datetime('now'), 'stamp', 0)",
                   (f"other{STAMP}@example.com", f"OTHER{STAMP}@EXAMPLE.COM", other_phone))
        other_id = db.execute("select last_insert_rowid()").fetchone()[0]
        db.execute("insert into Addresses (UserId, FullName, Phone, AddressLine, IsDefault) values (?, 'Other', ?, 'Not yours', 1)",
                   (other_id, other_phone))
        other_address = db.execute("select last_insert_rowid()").fetchone()[0]
        db.commit()
        db.close()
        r = step(r"""
  const out = {};
  await page.goto(APP + '/Account/Profile');
  await fill(page, { FullName: 'Verify Renamed', Phone: '__OTHER_PHONE__' }); await submit(page);
  out.taken = await fieldErrors(page);
  await fill(page, { FullName: 'Verify Renamed', Phone: '__PHONE__' }); await submit(page);
  out.status = await text(page, '.account-status');
  out.nav = (await page.locator('.site-nav-account').innerText()).replace(/\n/g, ' | ');
  return JSON.stringify(out);
""".replace("__OTHER_PHONE__", other_phone).replace("__PHONE__", PHONE))
        check("profile refuses a phone another account uses",
              r["taken"] == ["This phone number is already used by another account."], str(r["taken"]))
        check("profile change is saved and shown in the nav",
              r["status"] == ["Your details were saved."] and r["nav"] == "Verify Renamed | Sign out", str(r))

        # 10. Addresses: default rules, edit, delete, someone else's address.
        r = step(r"""
  const out = {};
  const add = async (line, makeDefault) => {
    await page.goto(APP + '/Account/Addresses/Create');
    await fill(page, { Label: line, FullName: 'Verify User', Phone: '0911111111', AddressLine: line, Ward: 'Ben Nghe', District: 'District 1', City: 'Ho Chi Minh City' });
    if (makeDefault) await page.check('.account-form #IsDefault');
    await submit(page);
  };
  const defaultLabel = async () => (await page.locator('.address-item:has(.address-badge) .address-head strong').allTextContents());
  await page.goto(APP + '/Account/Addresses/Create');
  await page.click('.account-form button[type=submit]'); await page.waitForTimeout(300);
  out.emptyErrors = (await fieldErrors(page)).length;
  await add('First street', false);
  out.firstDefault = await defaultLabel();
  await add('Second street', true);
  out.secondDefault = await defaultLabel();
  await page.click('.address-item:has-text("First street") button:has-text("Make default")'); await page.waitForLoadState('load');
  out.backToFirst = await defaultLabel();
  await page.click('.address-item:has-text("Second street") a:has-text("Edit")'); await page.waitForLoadState('load');
  await fill(page, { AddressLine: 'Second street, floor 3' }); await submit(page);
  out.edited = (await text(page, '.address-text')).some(t => t.includes('floor 3'));
  await page.screenshot({ path: 'addresses.png', fullPage: true });
  await page.click('.address-item:has-text("floor 3") button:has-text("Delete")'); await page.waitForLoadState('load');
  out.afterDelete = (await page.locator('.address-item').count());
  out.otherEdit = (await page.request.get(APP + '/Account/Addresses/Edit/__OTHER__')).status();
  return JSON.stringify(out);
""".replace("__OTHER__", str(other_address)))
        check("empty address form is stopped in the browser", r["emptyErrors"] >= 3, str(r["emptyErrors"]))
        check("first address becomes the default", r["firstDefault"] == ["First street"], str(r["firstDefault"]))
        check("ticking default on a new address moves the default", r["secondDefault"] == ["Second street"], str(r["secondDefault"]))
        check("make default moves it back", r["backToFirst"] == ["First street"], str(r["backToFirst"]))
        check("address edit is saved", r["edited"], str(r["edited"]))
        check("address delete removes it", r["afterDelete"] == 1, str(r["afterDelete"]))
        check("another user's address is 404", r["otherEdit"] == 404, str(r["otherEdit"]))
        db = sqlite3.connect(TMP / "app.db")
        still = db.execute("select AddressLine from Addresses where Id = ?", (other_address,)).fetchone()
        db.close()
        check("another user's address is untouched", still == ("Not yours",), str(still))

        # 11. Signed-in and new pages: load, no sideways scroll, no console errors.
        r = step(r"""
  const consoleErrors = [];
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  const out = {};
  for (const [w, h] of [[1440, 900], [390, 844]]) {
    await page.setViewportSize({ width: w, height: h });
    for (const p of ['/Account', '/Account/Profile', '/Account/ChangePassword', '/Account/Addresses', '/Account/Addresses/Create']) {
      const resp = await page.goto(APP + p);
      out[p + '@' + w] = { status: resp.status(), scroll: await page.evaluate(() => document.documentElement.scrollWidth) };
    }
    if (w === 390) await page.screenshot({ path: 'addresses-mobile.png', fullPage: true });
  }
  // Still 390px wide: the account links sit in the collapsed menu, so open
  // it the way a phone user would, then sign out from there.
  await page.click('.navbar-toggler');
  await page.locator('.site-nav-account-out').waitFor({ state: 'visible' });
  out.mobileMenu = (await page.locator('.site-nav-account').innerText()).replace(/\n/g, ' | ');
  await page.screenshot({ path: 'mobile-menu.png' });
  await signOut(page);
  for (const [w, h] of [[1440, 900], [390, 844]]) {
    await page.setViewportSize({ width: w, height: h });
    for (const p of ['/Account/ForgotPassword', '/Account/ResetPassword?email=a%40example.com']) {
      const resp = await page.goto(APP + p);
      out[p + '@' + w] = { status: resp.status(), scroll: await page.evaluate(() => document.documentElement.scrollWidth) };
    }
  }
  await page.setViewportSize({ width: 1440, height: 900 });
  out.consoleErrors = consoleErrors;
  return JSON.stringify(out);
""")
        for key, v in r.items():
            if "@" in key:
                width = int(key.split("@")[1])
                check(f"page {key} loads without sideways scroll", v["status"] == 200 and v["scroll"] <= width, json.dumps(v))
        check("no console errors on the signed-in and password pages", not r["consoleErrors"], "; ".join(r["consoleErrors"]))
        check("on a phone the menu button reveals the name and sign out", r["mobileMenu"] == "Verify Renamed | Sign out", r["mobileMenu"])
        current_password = changed_password

        # 12. Lockout: 5 wrong passwords, then the right one.
        r = step(r"""
  const out = {};
  for (let i = 0; i < 5; i++) {
    await page.goto(APP + '/Account/Login');
    await fill(page, { Email: '__EMAIL__', Password: 'WrongPass1' }); await submit(page);
  }
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: '__EMAIL__', Password: '__PASSWORD__' }); await submit(page);
  out.msg = await errors(page);
  out.url = page.url().replace(APP, '');
  return JSON.stringify(out);
""".replace("__EMAIL__", EMAIL).replace("__PASSWORD__", current_password))
        check("right password after 5 wrong ones is locked out",
              r["msg"] == ["Too many failed attempts. This account is locked for a few minutes."] and r["url"].startswith("/Account/Login"), str(r))
        row = sqlite3.connect(TMP / "app.db").execute(
            "select AccessFailedCount, LockoutEnd, NormalizedEmail, length(SecurityStamp) from Users where Email = ?", (EMAIL,)).fetchone()
        check("database row has lockout end, normalized email and stamp",
              row is not None and row[1] is not None and row[2] == EMAIL.upper() and row[3] > 0, str(row))
    finally:
        stop_app(proc)

    # 13. Mail server unreachable: register says so and stays on the form.
    unreachable = {"Smtp__Host": "127.0.0.1", "Smtp__Port": "1", "Smtp__UserName": "u",
                   "Smtp__Password": "p", "Smtp__FromAddress": "store@example.com"}
    shutil.copy(WEB / "AppleStore.db", TMP / "app-smtp.db")
    proc = start_app("Development", TMP / "app-smtp.db", TMP / "app-smtp.log", unreachable)
    try:
        r = step(r"""
  const out = {};
  await page.goto(APP + '/Account/Register');
  await fill(page, { Email: 'smtp__STAMP__@example.com', FullName: 'Smtp Check', Phone: '', Password: 'Password1', ConfirmPassword: 'Password1' });
  await submit(page);
  out.url = page.url().replace(APP, '');
  out.msg = await errors(page);
  out.kept = await page.inputValue('.account-form #Email');
  await page.screenshot({ path: 'smtp-failed.png' });
  return JSON.stringify(out);
""".replace("__STAMP__", STAMP))
        check("an unreachable mail server shows a message and keeps the form",
              r["url"] == "/Account/Register" and r["msg"] == ["We could not send the email. Please try again in a moment."]
              and r["kept"] == f"smtp{STAMP}@example.com", str(r))
        logged = "Sending" in (TMP / "app-smtp.log").read_text()
        check("the failed send is in the server log", logged)
    finally:
        stop_app(proc)

    # 14. Server error (no tables): the page says so and offers a way back.
    proc = start_app("Production", TMP / "empty.db", TMP / "app-prod.log")
    try:
        r = step(r"""
  const out = {};
  await page.goto(APP + '/Account/Login');
  await fill(page, { Email: 'a@example.com', Password: 'Password1' });
  await page.click('.account-form button[type=submit]');
  await page.waitForLoadState('load');
  out.url = page.url().replace(APP, '');
  out.heading = await page.locator('h1').first().innerText();
  out.links = (await page.locator('main a, section a').allTextContents()).map(s => s.trim()).filter(Boolean);
  await page.screenshot({ path: 'server-error.png' });
  return JSON.stringify(out);
""")
        check("a server error shows the error page with a link back", r["heading"] != "" and len(r["links"]) > 0, json.dumps(r))
    finally:
        stop_app(proc)
        subprocess.run(["playwright-cli", "-s=" + SESSION, "close"], capture_output=True, cwd=TMP)

    failed = [n for n, ok in results if not ok]
    print(f"\n{len(results) - len(failed)} of {len(results)} checks passed. Screenshots and logs: {TMP}")
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
