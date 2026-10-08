# Finish plan (from 2026-10-05)

The single place that says what is done, what is next, and how each task is
finished. Read this first in every session, update it in the same PR as the
work. `docs/roadmap.md` keeps the milestone descriptions; this file keeps the
order and the status.

On 2026-10-05 the project owner asked to finish the project this week. The
submission deadline date is not known yet ("chưa rõ"), so the order below
puts a demoable buying flow and the admin area first.

## Ground rules (all agreed with the project owner)

1. **This is an ASP.NET course project.** When ASP.NET Core has a built-in
   mechanism, use it and name it in the plan: Identity (`UserManager`,
   `SignInManager`, roles, `[Authorize]`), MVC controllers and Razor views,
   tag helpers and model validation, EF Core Code First migrations, Areas for
   the admin side, dependency injection. Report chapter 2 is written around
   these.
2. **Reference:** a previous student's submission for the same course and
   instructor (TS. Đoàn Phước Miền), the owner's file
   `~/Downloads/chia sẽ khóa học.rar`: cover template, a 5-chapter Word
   report, and a .NET 9 MVC project using Identity. It is an example, not an
   official rubric. Check new features against it; do better where it is weak
   (it signs out with GET, allows 3-character passwords, has no lockout).
3. **Scope:** M1, M3, M4 (COD real, VNPay and MoMo simulated unless sandbox
   credentials appear), M5, M7, then the report. M6 and M8 only if time is
   left.
4. **OrderStatus:** Pending 0, Confirmed 1, Shipping 2, Completed 3,
   Cancelled 4 (decided 2026-10-05).
5. **Merging:** Claude merges its own PR into `dev` once build and tests are
   green. Commits are split by feature and by task.

## Definition of done for every task

1. PLAN gate (target behavior, assumptions, gaps, test plan), wait for the
   owner's yes. Name the ASP.NET mechanisms used.
2. Feature branch from `dev`, named in the table below.
3. Structural commit first if there are entities or migrations, then one RED
   commit and one GREEN commit per behavior. Each RED commit builds and fails
   only its new tests.
4. `dotnet build --no-incremental`, `dotnet test`,
   `dotnet format --verify-no-changes` all clean. Use the full rebuild:
   incremental builds hide warnings in projects they skip.
5. Live browser check with a rerunnable script under `setup/verify-*/`
   (pattern: `setup/verify-account/verify.py`): every page at 1440px and
   390px, console errors, every path including the failure ones, a server
   error ending on a page with a way back. Result recorded in
   `docs/verification.md`.
6. Docs updated in the same PR: this file, `docs/roadmap.md`,
   `docs/data-model.md` or `docs/architecture.md` when touched.
7. PR into `dev`, merged when green. `HANDOFF.md` entry at the end of the
   session.
8. Every week (Tuesday to Monday) has commits and a `progress-report/` file.

## Task order and status

| # | Task | Use cases | Branch | ASP.NET focus | Status |
|---|---|---|---|---|---|
| 1 | Resolve OrderStatus, week 3 report | | `chore/order-status-confirmed` | | done, PR #11 |
| 2 | Register, OTP, sign in, sign out, lockout | 1-3 | `feat/identity-login` | Identity with a custom `UserStore`, cookie, `[Authorize]`, anti-forgery | done, verified 31/31, PR #12 |
| 3 | Forgot password, change password, profile and addresses | 4-6 | `feat/account-password-profile` | `UserManager.ChangePasswordAsync`, `ResetPasswordAsync`, security stamp sign-out | done, verified 67/67, PR #13 |
| 3b | Send codes by real email (SMTP) | 2 | `feat/smtp-email` | options pattern with startup validation, user-secrets, MailKit | done, verified 69/69; real Gmail send confirmed by the owner (PR #14) |
| 4 | Admin area skeleton and seeded admin account | 25 (part) | `feat/admin-area` | Areas, `[Authorize(Roles = "Admin")]`, seeding through `UserManager`, options pattern | done, verified 31/31, PR #16 |
| 5 | Cart | 11-14 | `feat/cart` | `[Authorize]`, anti-forgery, DB cart with unique indexes (EF migration), view component for the nav count | done, verified 23/23, PR #17 |
| 6 | Checkout and voucher | 15-16 | `feat/checkout` | model validation, EF Core transaction with conditional writes, `TimeProvider`, voucher table by migration | done, verified 25/25, PR #18 |
| 7 | Payment: COD, VNPay and MoMo simulated | 17 | `feat/payment` | options pattern with `ValidateOnStart`, signed callbacks (HMAC-SHA512), conditional updates | done, verified 16/16, PR #19 |
| 8 | Order management (staff) and order tracking (customer) | 18-19, 22-24, 37 | `feat/orders` | `[Authorize(Roles = "Admin,Employee")]`, one rules table, compare-and-swap updates, partial views, email | done, verified 19/19, PR #20 |
| 9 | Admin product and voucher CRUD, account roles | 25-27, 29-31 | `feat/admin-products` | Areas, model binding, optimistic concurrency (row version), security stamp, unique index | done, verified 15/15, PR #21 |
| 10 | Revenue report | 33-36 | `feat/admin-reports` | LINQ aggregation, export | not started |
| 11 | Report (Word, 5 chapters) and cover | | `docs/thesis-report` | chapter 2 explains the mechanisms above | not started |
| 12 | Week 4 progress report (2026-10-07 to 10-13) | | with any PR that week | | started 2026-10-07 with task 4, update as the week goes |

## Open decisions (ask before the task that needs them)

| Decision | Needed by task | Notes |
|---|---|---|
| Report language and who writes which chapter | 11 | sample report is Vietnamese |

## Decided

| Decision | Date | Outcome |
|---|---|---|
| Forgot password | 2026-10-05 | 6-digit code in `UserTokens` (`ResetPasswordOtp`), because `docs/requirements.md` use case 4 says "forgot password, OTP". An unknown email is told plainly there is no account (registration already reveals taken emails). |
| Email change | 2026-10-05 | not offered; the email is the sign-in name |
| Number of addresses | 2026-10-05 | no limit |
| Admin catalog | 2026-10-08 | "delete" a product = take it off sale; admins edit products, variants and the main photo; account roles page (owner). No uploads: admins pick from the shop's own licensed photos, and Claude sources photos (owner) |
| Sessions | 2026-10-08 | security stamp checked on every request, so a role or password change ends other sessions at once (Claude's, to make role changes immediate as promised) |
| Order rules | 2026-10-08 | customer cancels while pending, staff until shipped; an online order is confirmed only once paid; delivered cash orders are recorded as paid; public tracking by order number and receiver phone (owner chose all four) |
| Staff access | 2026-10-08 | employees get `/Admin/Orders`, the dashboard stays admin-only; emails on placing and shipping (Claude's, stated before building) |
| Online payment | 2026-10-08 | no sandbox credentials: VNPay and MoMo are simulated inside the app (`/PaymentSimulator`), only when `Payments:Mode=Simulated` (Development); the app refuses to start with it in Production (owner agreed the approach) |
| Commits | 2026-10-08 | owner: keep going through the plan, split commits, update progress as work lands |
| Vouchers | 2026-10-07 | own table (`Vouchers`, `VoucherProducts`), owner's choice; three demo vouchers seeded until the admin pages (task 9) |
| Checkout assumptions | 2026-10-07 | shipping free (the store advertises it), COD only until task 7, no per-user voucher limit, order email with task 8 (Claude's, accepted) |
| Commits in task 6 | 2026-10-07 | owner allowed step-by-step commits for the checkout task |
| Cart needs an account | 2026-10-07 | a visitor is asked to sign in before adding, then returns to the same product choice (owner's choice; no guest cart to merge) |
| Commits in task 5 | 2026-10-07 | owner allowed committing step by step for the cart task only; ask again for later tasks |
| Admin account | 2026-10-07 | created at startup from `SeedAdmin` settings in user-secrets (environment variables outside Development); never promotes an existing customer |
| Senior review test rules | 2026-10-07 | adapted from the corjl webapp into `CLAUDE.md` at the owner's request (PR #15) |
| Real email | 2026-10-05 | Gmail SMTP with an App Password held in user-secrets on the owner's machine; without SMTP settings mail stays in the log |

## Known gaps carried forward

- No "resend code" button on the OTP page; an expired code means
  registering again.
- The lockout message reveals that an email has an account (Identity's
  default behavior).
- The Production error page renders without CSS when run from source and
  still has the template's "Development mode" paragraph (pre-existing, see
  `docs/verification.md`, 2026-10-05).
- Instructor collaborator acceptance not yet verifiable.
- Deleting an address asks no confirmation.
- No limit on how often a reset code can be requested.
- Real email works only on a machine whose user-secrets hold the SMTP
  settings; a deployed copy would need them as environment variables.
- Changing a user's role does not change their security stamp, so a
  demoted admin keeps the Admin role claim until the next stamp check (up
  to 30 minutes). No page changes roles yet; the task that adds one must
  call `UpdateSecurityStampAsync`.
- If the admin account is deleted or demoted while the `SeedAdmin`
  settings are still set, the next start creates it again.
- An online order left unpaid keeps its stock until someone cancels it;
  there is no time limit on paying.
- `/Track` has no limit on attempts; guessing needs both the order number
  and the receiver's full phone number. A rate limit would need a number
  from the owner.
- No rate limit on `/Track`, the registration and reset code forms; the
  built-in rate limiter needs numbers from the owner.
- "1.000" typed as a voucher discount reads as 1 under the server culture.
- No unique index on SKU (two retired AirTag variants share SKUs); two
  admins adding the same new SKU at the same moment could both succeed.
- Addresses, profile and opening checkout fall back to the global error page
  on a database failure.
- Five products on sale still have no photo: no licensed photo of the exact
  model exists yet (MagSafe Battery, Apple Watch Ultra 4, iPad Air 8 11"
  and 13", Apple Watch SE 3). Photos of the previous models exist; using
  them is the owner's call.
- A second successful payment on a paid order is recorded and logged as
  needing a refund; refunds themselves are manual.
- Checkout catches a database failure while placing; one while pricing
  (opening `/Checkout`) falls to the global error page, which has a way back.
- The cart count badge shows 0 for an empty cart rather than hiding.
- `AccountController` and `AddressesController` do not catch database
  failures themselves (the global error page does); the cart does.
- Live checks cannot run two requests truly in parallel on SQLite; the
  cart's race handling is proven by unit tests that stage the race.
- On macOS, Gmail sending needs `Smtp:CheckCertificateRevocation=false`
  (incomplete revocation check); whether to keep it off on the owner's
  machine is the owner's call.
