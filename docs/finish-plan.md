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
| 3b | Send codes by real email (SMTP) | 2 | `feat/smtp-email` | options pattern with startup validation, user-secrets, MailKit | code done, verified 69/69 with log-only mail; real Gmail send waits on the owner's App Password |
| 4 | Admin area skeleton and seeded admin account | 25 (part) | `feat/admin-area` | Areas, `[Authorize(Roles = "Admin")]`, seeding through `UserManager` | not started |
| 5 | Cart | 11-14 | `feat/cart` | session or DB cart, view components for the nav count | not started |
| 6 | Checkout and voucher | 15-16 | `feat/checkout` | model validation, transactions in EF Core | not started |
| 7 | Payment: COD, VNPay and MoMo simulated | 17 | `feat/payment` | options pattern for gateway config, callbacks | not started |
| 8 | Order management (staff) and order tracking (customer) | 18-19, 22-24 | `feat/orders` | role-based pages, status flow | not started |
| 9 | Admin product and voucher CRUD | 25-27, 29-31 | `feat/admin-products` | scaffolded CRUD on MVC, file upload | not started |
| 10 | Revenue report | 33-36 | `feat/admin-reports` | LINQ aggregation, export | not started |
| 11 | Report (Word, 5 chapters) and cover | | `docs/thesis-report` | chapter 2 explains the mechanisms above | not started |
| 12 | Week 4 progress report (2026-10-07 to 10-13) | | with any PR that week | | due by 2026-10-13 |

## Open decisions (ask before the task that needs them)

| Decision | Needed by task | Notes |
|---|---|---|
| Admin account: how its first password is set (user secrets, environment variable, or printed once at seed) | 4 | never a password committed to the repo |
| Vouchers: own table or configuration | 6 | the report's schema has no voucher table, only `Order.VoucherCode` |
| VNPay and MoMo sandbox credentials | 7 | without them both are simulated |
| Report language and who writes which chapter | 11 | sample report is Vietnamese |

## Decided

| Decision | Date | Outcome |
|---|---|---|
| Forgot password | 2026-10-05 | 6-digit code in `UserTokens` (`ResetPasswordOtp`), because `docs/requirements.md` use case 4 says "forgot password, OTP". An unknown email is told plainly there is no account (registration already reveals taken emails). |
| Email change | 2026-10-05 | not offered; the email is the sign-in name |
| Number of addresses | 2026-10-05 | no limit |
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
- Other sessions end within 30 minutes after a password change, not at
  once (Identity's default stamp check interval).
- Deleting an address asks no confirmation.
- No limit on how often a reset code can be requested.
- Real email works only on a machine whose user-secrets hold the SMTP
  settings; a deployed copy would need them as environment variables.
