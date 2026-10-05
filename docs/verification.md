# Verification log

Durable record of what was actually run and observed, so "it should work" never
substitutes for proof. Newest entry first. Append a new dated entry per
verification pass; do not edit or delete old ones, they are the audit trail.

## 2026-10-05: real email over SMTP (`feat/smtp-email`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 112 of 112 (10 new: 8 `EmailTests`, 2 in `AccountSettingsFlowTests`) |
| `python3 setup/verify-account/verify.py` | **69 of 69** |

`EmailTests` use real collaborators: the sender choice is resolved from a
real `ServiceCollection` and configuration, and the failure test opens a
real socket to a closed local port (refused at once, well under the 30
seconds the test allows). The two new live checks start the app with SMTP
pointed at that closed port: Register shows "We could not send the email.
Please try again in a moment.", keeps the typed email, and the server log
has the failure.

**Found during this pass:** the GREEN commit went in before
`dotnet format` was checked (the command chain did not stop on the format
error); a separate style commit fixed the one initializer. Also: once
user-secrets hold SMTP settings, a Development run would send real mail and
the live check could no longer read codes from the log; the script now sets
`Smtp__Host` to empty.

### Real send to a real inbox (same day)

The owner stored the Gmail settings in user-secrets (sending account
kbsaigonese@gmail.com, App Password typed by the owner). The app ran in
Development against a temp copy of the database and the Register form was
submitted in Chromium.

| Run | Result |
|---|---|
| 1. revocation check on (default) | refused in the TLS handshake: "An incomplete certificate revocation check occurred"; the page showed "We could not send the email..." and the log had the error |
| 2. same, outside the session sandbox | same error, so it is .NET on macOS, not this session's network |
| 3. `Smtp__CheckCertificateRevocation=false` for that run | sent to kbsaigonese@gmail.com in 5.4 s, page moved to the code step, no error logged |
| 4. same settings, to lebinh030199@gmail.com | sent in 3.4 s; **the owner confirmed the email arrived** |

Runs 1 and 2 also showed the failure path working for real: a visible
message, the typed email kept, the failure in the log. The code from the
email was not typed back into the app in this pass; that step is covered by
the live check script with log-only mail.

## 2026-10-05: use cases 4-6, forgot and change password, profile, addresses (`feat/account-password-profile`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 102 of 102 (27 new: 9 `PasswordResetServiceTests`, 6 `ProfileServiceTests`, 12 `AccountSettingsFlowTests` through the real app) |
| `python3 setup/verify-account/verify.py` | **67 of 67** live browser checks (the 31 from the first entry plus 36 new) |

New live checks: login links to forgot password; unknown email is told so
with a register link; wrong reset code stays; right code signs in with
"Your password was reset."; afterwards the old password fails and the new
works; change password refuses a wrong current password, keeps the session,
and the changed password signs in; profile refuses another account's phone
and shows the new name in the nav; first address is the default, ticking
default moves it, "Make default" moves it back, edit and delete work;
another user's address is 404 and its row is untouched; Account, Profile,
ChangePassword, Addresses, Addresses/Create, ForgotPassword, ResetPassword
load at 1440px and 390px with no sideways scroll and no console errors; on a
phone the menu button reveals the name and sign out.

**Found and fixed during this pass:**
1. Address defaults were first cleared with `ExecuteUpdate`, which left the
   tracked rows stale, so a later "make default" in the same context was not
   saved. Caught by `ProfileServiceTests`; defaults are now set on tracked
   rows and saved once.
2. "Add an address" (a link styled as a button) was underlined; seen in the
   screenshot, fixed in CSS.
3. The first live run timed out signing out at 390px: the sign-out button is
   inside the collapsed menu, as designed. The script now opens the menu
   button first, which also checks that phone users can reach it.

**Not checked:** that other sessions end after a password change. Identity
checks the stamp every 30 minutes by default, so a live check would have to
wait that long; the unit test checks that the stamp changes.

## 2026-10-05: M1 account pages on ASP.NET Core Identity (`feat/identity-login`)

**Scope checked:** build, format, full test suite, migration on the real dev
database, and a live browser pass over every account page and path.

### Build, format, test, migration

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors (a full rebuild found one CS8625 warning that incremental builds had hidden; fixed in its own commit) |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 75 of 75 pass (53 existing, 22 new: 7 `UserStoreTests`, 4 new `RegistrationServiceTests`, 11 `AccountFlowTests` running the real app through `WebApplicationFactory`) |
| `dotnet ef database update` on `src/AppleStore.Web/AppleStore.db` | applied `AddIdentityColumnsToUsers`, 4 new columns present |

### Live browser check

Rerunnable script: `python3 setup/verify-account/verify.py`. It starts the app
against a temp copy of the dev database (the dev database is not written to),
drives Chromium with `playwright-cli`, reads each OTP from the app log, and
prints one line per check. Result: **31 of 31 checks passed.**

| Area | Checks |
|---|---|
| Pages | Login, Register, AccessDenied: HTTP 200 at 1440px and 390px, no sideways scroll, 0 console errors |
| Redirects | anonymous `/Account` goes to sign in with `ReturnUrl`; `/Account/VerifyOtp` with no registration goes to Register |
| Register | empty form and mismatched passwords stopped in the browser with no POST sent; short password shows the 8-character rule; same email in other case refused; same phone refused |
| Code page | wrong code refused; right code creates the user and signs them in; reusing a spent code says expired and offers Register again or sign in |
| Sign in | wrong password and unknown email show the identical message; upper-case email works; local return URL followed; external return URL lands on `/`; cookie is a session cookie without "remember me" and persistent (about 14 days) with it |
| Sign out | GET `/Account/Logout` is 404 and keeps the session; POST without anti-forgery token is 400; the nav button signs out |
| Lockout | right password after 5 wrong ones is refused with the lockout message; the row has `LockoutEnd` set, upper-case `NormalizedEmail`, a 32-character `SecurityStamp` |
| Server error | app in Production against an empty database: sign in ends on "Something went wrong" with a "Back to home" link, not a hang |

**Observed and explained, not bugs:**
- After lockout the row shows `AccessFailedCount = 0`. Identity resets the
  count when it sets `LockoutEnd`; the lock itself is `LockoutEnd`.
- The Production error page renders without CSS. That is the
  run-from-source Production gotcha already in `docs/architecture.md`
  ("Running locally"): static assets are only served from the Development
  manifest or a published build. It predates this branch. The page also
  still carries the template's "Development mode" paragraph, also
  pre-existing (`Views/Shared/Error.cshtml` is not touched here).

### Found and fixed during this pass

1. Two pending registrations for one email: confirming the second crashed
   with `DbUpdateException` (HTTP 500) on the base branch. Now it returns
   "already exists". Covered by `RegistrationServiceTests`.
2. Opening the code page without a registration showed a form that could
   only fail with no visible message. Now it redirects to Register.
   Covered by `AccountFlowTests`.

### Not checked in this pass

- Forgot password, change password, update profile: not built yet.
- No admin account exists yet, so `[Authorize(Roles = "Admin")]` has no
  page to check; that comes with M7.

## 2026-09-17: scaffold plus M1 registration service

**Scope checked:** full solution build, full test suite, and a live browser
check of the running app (no custom UI exists yet, see below).

### Build and test

```
dotnet build
  Build succeeded.
  0 Warning(s)
  0 Error(s)

dotnet test
  Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11
```

The 11 tests: 1 schema smoke test (`SchemaSmokeTests`, all 24 entity
configurations create a schema cleanly against SQLite in-memory), 4
`OtpServiceTests`, 6 `RegistrationServiceTests`.

### Migration

`dotnet ef database update --project src/AppleStore.Infrastructure --startup-project src/AppleStore.Web`
against a real (non-in-memory) SQLite file: applied cleanly, all 24 tables and
their indexes created, `__EFMigrationsHistory` recorded the migration. Confirms
the migration is not just internally consistent (which the smoke test already
proves) but actually applies outside a test fixture.

### Live browser check

Launched `dotnet run` from `src/AppleStore.Web`, drove it with a headless
Chromium via Playwright (no `chromium-cli` or project run-skill was available on
this machine at the time; see the `run` skill's fallback pattern), hit the two
pages that exist:

| Page | HTTP status | Console errors |
|---|---|---|
| `/` (home) | 200 | 0 |
| `/Home/Privacy` | 200 | 0 |

**What was on screen:** the default ASP.NET Core MVC template pages
(`AppleStore.Web` nav bar, "Welcome" heading, unedited Privacy placeholder). This
is correct for the current state, not a gap in verification: only
`OtpService` and `RegistrationService` exist so far (see `docs/roadmap.md`),
neither of which has a controller or view yet.

**Found during this check, not a bug:** the first attempt launched with
`--no-launch-profile --urls http://localhost:5286` to pin a specific port, which
skips `launchSettings.json` and so never sets `ASPNETCORE_ENVIRONMENT=Development`.
The scoped-CSS bundle the default layout references only serves from the
Development-time static web assets manifest, so every page 500'd loading its
stylesheet. Re-ran with `ASPNETCORE_ENVIRONMENT=Development` set explicitly and
got the zero-error result above. A plain `dotnet run` (no `--no-launch-profile`
override) already sets this correctly. Documented in
`docs/architecture.md`, "Running locally," so the next person who needs a
specific port does not lose time on the same thing.

**Screenshots:** captured with a throwaway Playwright script, not committed to
the repo (they would go stale the moment real UI exists, and the repo has no
mechanism yet for keeping screenshot fixtures current). Published as a Claude
artifact for that session's review; that link is session-scoped, not a permanent
project asset, treat this written record as the source of truth going forward
rather than the link.

### Not checked in this pass

- No UI exists to click through beyond the two default pages above.
- Login, forgot password, change password, update profile: not built yet.
- E1-E5 error-path walk (network drop, backend error, slow response, unmount
  mid-flight, not-yet-loaded) from the global error-handling mandate does not
  apply yet either, there is no async UI surface to walk.
