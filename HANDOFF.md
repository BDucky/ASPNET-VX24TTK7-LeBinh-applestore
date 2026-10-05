# HANDOFF: Apple Store course project

Newest entry first. Each entry is one session's worth of work: what landed, what
was decided and why, and exactly where to pick up next. Do not edit or delete old
entries when adding a new one, prepend instead, they are the record of how the
project actually got here.

## 2026-10-05 (evening): real email over SMTP

**What landed.** Branch `feat/smtp-email` (PR open, not merged until a real
email is confirmed): `SmtpEmailSender` (MailKit), `SmtpOptions` bound and
validated at startup, `AddAppleStoreEmail` as the one place that picks the
sender (SMTP when `Smtp:Host` is set, log-only otherwise), and a visible
"could not send" message on Register and Forgot password. 111 tests; live
check 69 of 69, including an unreachable mail server. The Web project has a
UserSecretsId; the live check forces log-only mail so it never emails anyone.

**Decisions:** owner wants codes sent for real; Gmail SMTP with an App
Password (Claude's recommendation, accepted). The password goes into
user-secrets typed by the owner, never seen by Claude or committed. The
review gallery of the new account pages was published as a private artifact
for the owner; the owner approved it.

**Where to pick up:**
1. Owner runs the `dotnet user-secrets set` commands from the root README.
2. Start the app, register or use Forgot password with a real inbox the
   owner can read, confirm the email arrives, record it in
   `docs/verification.md`, merge the PR.
3. Then task 4 in `docs/finish-plan.md` (Admin area); its plan needs the
   owner's choice of how the first admin password is set.

## 2026-10-05 (later): M1 finished, use cases 4-6

**What landed.** PR #13, `feat/account-password-profile`: forgot password by
emailed code (stored hashed in `UserTokens`, Identity's `ResetPasswordAsync`),
change password, profile and delivery addresses. 102 of 102 tests; live check
`setup/verify-account/verify.py` 67 of 67 on the final commit. M1 (use cases
1-6) is complete.

**Decisions:** owner chose the emailed code over a reset link because
`docs/requirements.md` use case 4 says OTP. Owner rejected the "same message
whether or not the email exists" idea as bad UX; it also protected nothing,
since registration already reveals taken emails. Email change not offered,
no limit on addresses (Claude's assumptions, accepted).

**Where to pick up:** task 4 in `docs/finish-plan.md`, the Admin area and a
seeded admin account. Its PLAN gate needs the open decision on how the first
admin password is set.

## 2026-10-05: finish push starts, OrderStatus resolved, M1 sign-in on Identity

**Read `docs/finish-plan.md` first.** It holds the task order, the status of
each task, the definition of done, and the open decisions. This entry is the
story of how it got there.

**What landed.**
1. PR #9 and PR #10 (catalog and storefront) merged into `dev`.
2. PR #11: `OrderStatus` gets its fifth value, `Confirmed` (Pending 0,
   Confirmed 1, Shipping 2, Completed 3, Cancelled 4), plus the week 3
   progress report.
3. `feat/identity-login`: use cases 1-3 on ASP.NET Core Identity. A custom
   `UserStore` keeps the report's own `Users` table (4 Identity columns added
   by migration), `AppUserClaimsPrincipalFactory` puts the role and full name
   in the cookie, `AccountController` has register, verify code, sign in,
   sign out, and the account page. 74 of 74 tests; live browser check
   `setup/verify-account/verify.py` 31 of 31.

**Decisions made this session, and whose call each was:**

| Decision | Who decided | Why |
|---|---|---|
| Finish the project this week; scope M1, M3, M4, M5, M7, then the report | owner | deadline near, date unknown |
| Claude merges its own PRs into `dev` when green | owner | standing rule from 2026-10-05 |
| OrderStatus fifth value is Confirmed, values shifted | owner | matches the order-tracking use case; no order rows existed |
| Use ASP.NET Core Identity, not a hand-rolled cookie | owner ("đây là dự án asp.net") | the course grades ASP.NET mechanisms; the sample report has a chapter on Identity |
| Identity over the existing `Users` table with a custom store, not `IdentityUser` and `AspNet*` tables | Claude recommended, owner approved | keeps the report's 24-table schema and keeps `Domain` free of ASP.NET types |
| Password 8 characters minimum, no other rule; lockout 5 failures for 5 minutes | Claude proposed, owner approved | the report asks for a limit without numbers; these are Identity's defaults |
| `NormalizedEmail` added as a fourth column (plan said three) | Claude, reported after | Identity looks users up by it; it also fixed the case-sensitive duplicate check |
| UI text stays English | Claude, following the existing storefront | the whole site is English today; the report can still be Vietnamese |

**Found on the way:** on the base branch, two pending registrations for one
email crashed the second confirmation with a 500. Fixed and covered by a test.

**Where to pick up:** task 3 in `docs/finish-plan.md` (forgot password,
change password, profile). Start with the PLAN gate; it needs the
forgot-password decision listed there.

## 2026-09-22: course submission closed out, PRs merged (session 2 continued)

**What landed, on top of the entry below.** Confirmed this is an individual
project, not a group one, dropped all "Nhom 05" team framing across the repo.
Repo rename to `ASPNET-VX24TTK7-LeBinh-applestore` confirmed done. Branch
protection configured on `main` and `dev` (require PR before merge, block
force pushes, restrict deletions), verified via the rules API. Instructor
invited as a collaborator (GitHub resolved the email to `nguyennhutlam`),
pending their acceptance. Removed other-codebase references from
working-tree files (`.claude/output-styles/*`, `.claude/commands/git/
summary-merges.md`, this file). PR #2 (M1's `OtpService`/`RegistrationService`)
and PR #5 (course submission requirements) both merged into `dev`.

**Decision:** left 7 already-merged commit messages as-is for now (they
still name another codebase in their text, e.g. "port dev workflow hooks
from corjl-webapp"). Rewriting them needs a `git filter-branch` pass plus a
force-push to `main`/`dev`, which needs repo admin; this session's account
doesn't have it and adding it hit GitHub's actual permission model (only an
existing admin can grant admin, confirmed via a 404 on the collaborators
API, not a workaround-able limitation). The full runbook (message text per
commit, filter-branch invocation, ruleset toggle, force-push, cleanup) was
handed to the user to run themselves as `BDucky`, in their own terminal, no
session access changes needed. Purely cosmetic, not blocking; skip unless
asked.

**Still open, exactly where to pick up:**
1. The 7-commit message rewrite above, if the user still wants it.
2. Instructor acceptance of the invite, not in anyone's control from here.
3. Next real task, per `docs/roadmap.md`: finish M1 (Auth/OTP), the
   register/verify-OTP controller and views, log in, forgot password,
   change password, update profile are all still unstarted. This is
   testable service/controller logic, start with the TDD plan-gate per
   `CLAUDE.md`, in a fresh session.

## 2026-09-22: course submission requirements (GitHub management, directory tree)

**What landed.** The course brief's section 4 (GitHub project management,
photographed and pasted by the user) requires: a specific repo naming syntax,
inviting the instructor as a collaborator, a continuously-updated README, weekly
commits, a `progress-report/` folder, and a specific top-level directory tree
(`setup/`, `scr/`, `progress-report/`, `thesis/{doc,pdf,html,abs,refs,soft,docker}`).
Added `docs/submission.md` transcribing the full requirement and tracking
compliance, scaffolded the required folders (each with a README explaining its
purpose; `scr/` points at the real `src/` tree rather than duplicating it, `soft/`
and `docker/` left uncreated since the brief marks them "if any"), updated
`README.md` with a repository-organization section and a team-contact section,
and added a "Course Submission Requirements" section to `CLAUDE.md`.

**Decisions made this session, and whose call each was:**

| Decision | Who decided | Why |
|---|---|---|
| Target repo name `ASPNET-VX24TTK7-LeBinh-applestore` | malop (`VX24TTK7`) and name (`LeBinh`), user, asked directly; shortname (`applestore`), Claude's call, explicitly delegated ("choose this yourself") | matches the brief's required syntax |
| Rename and instructor invite documented but not executed | forced by access, not a choice | the session's authenticated GitHub account (`binhlerowboatsoftware`) has push/triage on `BDucky/apple-store`, not admin (confirmed via the collaborators API); both actions need admin, and the invite also needs the instructor's GitHub username, which isn't known yet |
| `scr/` kept as a thin pointer folder, not a duplicate source tree | Claude's call, stated in the plan | the brief's own diagram names the source folder `scr`, almost certainly a typo for `src`; duplicating the real source under a second name would drift immediately |
| `soft/` and `docker/` not created yet | Claude's call, following the brief's own "if any" caveat | nothing exists yet to put in either; per the No Self-Assumption rule, don't scaffold folders nobody asked for |
| README team-contact section left as a placeholder | forced, not a choice | the user hadn't yet clarified this is an individual project, not a group one; resolved in a later session, see the 2026-09-22 course-submission entry above |

**Still open, exactly where to pick up:**
1. Whoever has admin on `BDucky/apple-store` (BDucky) needs to run the rename
   and (once the instructor's GitHub username is known) the collaborator invite;
   exact commands are in `docs/submission.md`.
2. The user still owes the full team contact list (name/email/phone per member)
   for the README "Team contact" section, currently just Le Binh's own email.
3. First weekly `progress-report/` entry is still due; the folder only has its
   README so far.
4. This work sits on branch `docs/github-submission-requirements`, not yet
   pushed or opened as a PR into `dev`.

## 2026-09-17: repo init, branching, M1 registration service (session 1)

**What landed.** The repository did not exist before this session. Built from a
63-page course report (`docs/requirements.md` has the full transcription):
TVU, "Cong nghe phan mem," 2025, an e-commerce site selling Apple
products, 4 actors, 37 use cases, a 24-table SQL Server schema. The source
report's own byline names a group ("Nhom 05"); this is an individual
submission built from that report's requirements.

1. Solution scaffold: `AppleStore.Domain` / `AppleStore.Infrastructure` /
   `AppleStore.Web` / `AppleStore.Tests`, all 24 entities plus EF Core
   configuration plus the initial migration, docs, ported dev tooling from
   an existing dev-tooling reference. 82 commits, pushed straight to `main` (see below, that was
   before the branching rule existed).
2. `main` and `dev` branches created; `dev` is now the integration target.
   Branch protection could not be configured (this account has push/triage on
   `BDucky/apple-store`, not admin; see `docs/branching.md` for the exact GitHub
   UI steps for whoever has admin).
3. `OtpService` and `RegistrationService` (M1's first slice), test-first: RED
   commit then GREEN commit per behavior, 10 new tests. PR #2,
   `feat/register-account` into `dev`, open.
4. Verified live: build, full test suite, migration applied against a real
   SQLite file, app driven in a headless browser. Full detail and numbers in
   `docs/verification.md`.

**Decisions made this session, and whose call each was:**

| Decision | Who decided | Why |
|---|---|---|
| ASP.NET Core MVC (Razor, one Web project), not API+SPA or API+Blazor | user, asked directly | course-project simplicity |
| Scaffold and tooling only this session, no feature UI | user, asked directly | matches "one task per session" |
| SQLite for local dev, SQL Server-shaped schema | user said "free and no cost"; SQLite was my call given no Docker on this machine | zero install friction, swap path documented in `docs/architecture.md` |
| `main`/`dev` branch model, feature branches PR into `dev` | user, asked directly, after the scaffold had already been pushed to `main` | "as professional as possible," stop pushing to `main` |
| Registration OTP held in `IMemoryCache`, not `UserTokens` (option A) | my recommendation, user said "proceed" | schema's `UserTokens.UserId` FK has nothing to attach to before the `User` row exists; avoids a schema change |
| OTP: 6 digits, 5-minute expiry; `PasswordHasher<User>` for hashing | my assumption, stated in the PLAN, not corrected | report does not specify a duration; `PasswordHasher` is the ASP.NET Core standard, no new dependency |
| Business services live in `Infrastructure/Services/`, no separate `Application` project | my call, made when `RegistrationService` needed a home | project size does not justify a 5th project yet, documented as reversible in `docs/architecture.md` |
| `docs/qa.md`, `workflow/*.md`, `lint/*.md`, `test/e2e.md`/`test/sync.md` NOT ported | my call, deviating from what I'd told the background agent to do | all hard-wired to a Jira- and Nx-affected-tooling-based SDLC pipeline; porting "adapted" would have fabricated a whole SDLC system nobody asked for |

**Known gaps, still open, do not resolve silently:**

- `OrderStatus` enum: report's schema says the value domain is "0 to 4" but only
  names 4 values; a separate use case lists 6 narrative states. Full detail in
  `docs/data-model.md`. Must be resolved before M5 (order management) starts.
- Vouchers and price-history have no dedicated table in the report's schema
  chapter; design question for M3 and M6 respectively (`docs/roadmap.md`).
- Branch protection on `main`/`dev`: rules are written up in `docs/branching.md`
  but nobody with admin on the GitHub repo has applied them yet.
- PR merge policy was never actually settled: I asked twice (after PR #1 and
  again after PR #2) and got "merge it" for PR #1 specifically, not a standing
  answer. Ask again or get an explicit standing rule before assuming for PR #2
  onward.

**Where to pick up:**

1. If continuing M1: build the register + verify-OTP controller and views on
   top of `RegistrationService` (PR #2 still open at end of session), then log
   in, forgot password, change password, update profile in that order (matches
   `docs/roadmap.md`'s use-case ordering).
2. Read `docs/verification.md` before re-verifying anything, it already has the
   `ASPNETCORE_ENVIRONMENT=Development` gotcha documented so you do not lose
   time rediscovering it.
3. `docs/requirements.md`, `docs/data-model.md`, `docs/architecture.md`,
   `docs/roadmap.md`, `docs/branching.md` are all current as of this entry. If a
   later session changes something they describe, update the doc in the same
   PR, do not let them drift.
