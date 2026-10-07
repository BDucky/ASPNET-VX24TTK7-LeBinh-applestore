"""Shared harness for the live browser checks under setup/verify-*/.

One Harness per script: it starts the real app on its own port against a
throwaway database, drives Chromium through playwright-cli, and records one
PASS/FAIL line per check. Each script keeps only its own checks.
"""
import json
import os
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WEB = ROOT / "src" / "AppleStore.Web"

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


class Harness:
    def __init__(self, name, port):
        self.app = f"http://localhost:{port}"
        self.tmp = Path(tempfile.mkdtemp(prefix=name + "-"))
        self.session = name
        self.results = []

    def check(self, name, ok, detail=""):
        self.results.append((name, bool(ok)))
        print(f"{'PASS' if ok else 'FAIL'}  {name}" + (f"  ({detail})" if detail else ""))

    def start_app(self, env_name, db_path, log_path, extra_env=None):
        # Smtp__Host empty keeps mail in the log even when user-secrets hold
        # real SMTP settings, so a script can read each code and never mails
        # anyone. SeedAdmin blank for the same reason: user-secrets never seed
        # an admin into a check that did not ask for one.
        env = dict(os.environ, ASPNETCORE_ENVIRONMENT=env_name, ConnectionStrings__Default=f"Data Source={db_path}",
                   Smtp__Host="", SeedAdmin__Email="", SeedAdmin__Password="")
        env.update(extra_env or {})
        log = open(log_path, "w")
        proc = subprocess.Popen(
            ["dotnet", "run", "--no-build", "--no-launch-profile", "--urls", self.app],
            cwd=WEB, env=env, stdout=log, stderr=subprocess.STDOUT)
        for _ in range(60):
            try:
                urllib.request.urlopen(self.app + "/Account/Login", timeout=2)
                return proc
            except urllib.error.HTTPError:
                return proc
            except Exception:
                time.sleep(1)
        proc.kill()
        sys.exit("app did not start, see " + str(log_path))

    @staticmethod
    def stop_app(proc):
        proc.terminate()
        try:
            proc.wait(timeout=15)
        except subprocess.TimeoutExpired:
            proc.kill()

    def open_browser(self):
        subprocess.run(["dotnet", "build", "-v", "q", str(WEB)], check=True, capture_output=True)
        subprocess.run(["playwright-cli", "-s=" + self.session, "open"], capture_output=True, cwd=self.tmp)

    def close_browser(self):
        subprocess.run(["playwright-cli", "-s=" + self.session, "close"], capture_output=True, cwd=self.tmp)

    def run(self, js):
        path = self.tmp / "step.js"
        path.write_text(js)
        out = subprocess.run(["playwright-cli", "-s=" + self.session, "--raw", "run-code", "--filename=" + str(path)],
                             capture_output=True, text=True, cwd=self.tmp)
        text = out.stdout.strip()
        try:
            value = json.loads(text)
            # Steps return JSON.stringify(...), which --raw prints as a JSON string.
            return json.loads(value) if isinstance(value, str) else value
        except json.JSONDecodeError:
            sys.exit("browser step failed:\n" + text + out.stderr)

    def step(self, body):
        return self.run("async page => {\n" + LIB.replace("__APP__", self.app) + body + "\n}")

    def finish(self):
        failed = [n for n, ok in self.results if not ok]
        print(f"\n{len(self.results) - len(failed)} of {len(self.results)} checks passed. Screenshots and logs: {self.tmp}")
        sys.exit(1 if failed else 0)
