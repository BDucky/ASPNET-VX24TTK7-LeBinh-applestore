# HANDOFF: Apple Store course project

Newest entry first. Each entry is one session's worth of work: what landed, what
was decided and why, and exactly where to pick up next. Do not edit or delete old
entries when adding a new one, prepend instead, they are the record of how the
project actually got here.

## 2026-09-17: repo init, branching, M1 registration service (session 1)

**What landed.** The repository did not exist before this session. Built from a
63-page course report (`docs/requirements.md` has the full transcription):
HCMUTE, "Cong nghe phan mem," Nhom 05, 2025, an e-commerce site selling Apple
products, 4 actors, 37 use cases, a 24-table SQL Server schema.

1. Solution scaffold: `AppleStore.Domain` / `AppleStore.Infrastructure` /
   `AppleStore.Web` / `AppleStore.Tests`, all 24 entities plus EF Core
   configuration plus the initial migration, docs, ported dev tooling from
   `corjl-webapp`. 82 commits, pushed straight to `main` (see below, that was
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
| `docs/qa.md`, `workflow/*.md`, `lint/*.md`, `test/e2e.md`/`test/sync.md` from corjl-webapp NOT ported | my call, deviating from what I'd told the background agent to do | all hard-wired to Corjl's own ADWS `.ai/specs/` pipeline, Jira, and Nx-affected tooling; porting "adapted" would have fabricated a whole SDLC system nobody asked for |

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
