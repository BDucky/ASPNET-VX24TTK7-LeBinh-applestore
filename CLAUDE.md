# Apple Store

ASP.NET Core MVC web application selling Apple technology products. Course project,
TVU, Chuyên đề ASP.net, 2026, individual project. Source requirements live in
`docs/requirements.md`, the schema in `docs/data-model.md`.

## Writing for Humans

Applies to every word a person reads: `.md` files, spec and handoff docs, commit
messages, code comments, and replies in the terminal.

- Never use an em dash. Use a comma, a colon, a period, or parentheses, or rewrite
  the sentence.
- No symbolic arrows (arrow characters) or decorative emoji in prose. Write the
  words. Arrows stay allowed inside diagrams and path mappings, where they are
  notation rather than punctuation.
- Write the way a person writes. Plain sentences. No filler superlatives, no
  restating a point already made.
- Ordinary hyphens in compound words are fine.

This file follows its own rule. Do not reintroduce em dashes when editing it.

## Current Plan

Read `docs/finish-plan.md` at the start of every session. It is the source of
truth for task order, status, the definition of done, and open decisions.
Update it in the same PR as the work. This is an ASP.NET course project: when
ASP.NET Core has a built-in mechanism for something (Identity, Areas, model
validation, tag helpers, EF Core migrations), use it.

## Tech Stack

| Layer | Choice |
|---|---|
| Web framework | ASP.NET Core MVC, Razor views |
| ORM | Entity Framework Core, Code-First |
| Database (dev) | SQLite, file `AppleStore.db`, gitignored |
| Database (schema shape) | SQL Server types (`nvarchar`, `decimal(12,2)`, `datetime2(0)`), see `docs/architecture.md` for the SQLite-now / SQL-Server-later plan |
| Tests | xUnit |
| Migrations tool | `dotnet-ef`, installed as a local tool (`dotnet tool restore`) |

## Project Structure

- `src/AppleStore.Domain/` entities and enums only, zero external dependencies.
- `src/AppleStore.Infrastructure/` `AppDbContext`, `IEntityTypeConfiguration<T>`
  classes under `Data/Configurations/`, EF Core migrations.
- `src/AppleStore.Web/` controllers, views, `wwwroot/`, `Program.cs`, `appsettings*.json`.
- `tests/AppleStore.Tests/` xUnit, references Domain and Infrastructure.

Dependency direction: `Web -> Infrastructure -> Domain`, and `Web -> Domain` directly
for view models. Never let `Domain` reference EF Core or ASP.NET types.

## TDD Project Bindings

The global TDD-by-default flow in `~/.claude/CLAUDE.md` applies here. These are the
dotnet-specific commands it resolves to in this repo:

- Regression net (RED/GREEN/VERIFY): `dotnet test`
- Lint equivalent: `dotnet format --verify-no-changes` (run `dotnet format` to fix)
- Typecheck equivalent: `dotnet build` (the C# compiler is the type checker)
- Combined verify gate before delivering: `dotnet build && dotnet test`

Entity classes, EF configuration, and migrations are structural, not testable logic.
Treat them like config in the global "SKIP TDD" list. TDD applies once a task adds a
service, controller action with a decision in it, validator, or calculation
(voucher math, stock updates, revenue totals).

## Test Like a Senior Reviewer: Project Bindings

Adapted on 2026-10-07 from the rules the owner keeps for the corjl webapp
(`CLAUDE.local.md` there), at the owner's request. The global rule "Review
Like a Senior Reviewer" in `~/.claude/CLAUDE.md` still applies in full; this
section adds the steps that make it concrete here.

1. **Triage by risk, not size.** Pick one lane before planning:
   - Fast: copy, CSS, config, docs. Do it, then a live check.
   - TDD: anything with a decision (service, controller action that
     branches, validator, seeder, money or stock math). Full RED, GREEN,
     REFACTOR.
   - Live-only: layout and visuals. Verify with the browser script, and move
     any logic inside it into the TDD lane.
   - Spike first: root cause unknown. Throwaway repro first, then RED the
     confirmed behavior. Never lock a guess into a test.
   A change that touches money, authorization, startup, or shared code is
   TDD regardless of how small it is.
2. **Blast-radius sweep, in the PLAN and again before delivering.** List
   every place that renders or triggers the behavior (views, partials,
   controllers, startup, tests' host) and every condition that gates it
   (role, lockout, configuration source, environment, a database that is
   not migrated yet). Write it as a table: site, gating condition, verified
   or assumed. Two sites carrying the same rule is a finding (see "Extend,
   Do Not Copy").
3. **Find the contracts before writing tests.** Name the owner of each
   decision the change relies on (`UserManager`, `UserStore`, the cookie
   handler, EF Core's unique index) and let it run for real in the test.
   Fake only the edge (email, payment gateway), and make the fake enforce
   the owner's real rule.
4. **The PLAN holds:** approach, target behavior, blast radius, assumptions
   (with whose call each is), edge cases, error paths, blockers, test plan.
   Approval is on the approach, not only the behavior.
5. **Three paths per behavior:** happy, unhappy (bad input, missing
   configuration, failing dependency), and the golden path the user takes
   end to end in the real app.
6. **Attack sequences first.** Before writing tests, write the sequences
   that could end in wrong data, a page with no way back, or a silent
   mismatch (run twice, run concurrently, run before the database exists,
   configuration present in one environment and not another). Each one
   becomes a test or a written reason why it cannot happen.
7. **Minimal seam.** Make the test pass through the existing seam. Do not
   build new machinery only to make something testable.
8. **Audit before delivering:**
   - L4: re-run the blast-radius sweep. Did a site or condition appear that
     the tests do not cover?
   - L5: write down what was not tested, and say it in the reply.
   - L6: look for the same bug elsewhere and fix the class, not the
     instance. Any bug that escaped becomes a permanent RED test.
9. **Green is not done.** Deliver only after `dotnet build --no-incremental`,
   `dotnet test`, `dotnet format --verify-no-changes`, and the live browser
   script under `setup/verify-*/` with its failure paths, all with results
   shown.

## Extend, Do Not Copy: C# Bindings

The hard rule lives in the global `~/.claude/CLAUDE.md`. In this codebase:

- Never duplicate controller or service logic across two actions. Extract a shared
  private method, a base controller, or a service class instead.
- Prefer extension methods over copy-pasted helper blocks.
- Repeated conditional branching on a type or role (`if (user.Role == ...)` in
  several places) is a copy in disguise. Use a strategy/lookup table
  (`Dictionary<UserRole, ...>` or a small interface with one implementation per
  role) instead.
- A `switch` on an enum is fine as the single dispatch point; five scattered
  `if (status == X)` checks across files are not.

## Error Handling

- Every action that touches the database or an external service (email, payment
  gateway) wraps failure paths explicitly. No empty catch blocks.
- Use `ILogger<T>` for all logging. Never `Console.WriteLine`.
- For API-style JSON endpoints, return `ProblemDetails` on error, not a raw
  exception message.
- A user-facing action must never leave the page in a stuck state (spinner that
  never resolves, disabled button with no recovery). Every failure needs a visible
  message and a way forward (retry, go back, contact support).

## Git Workflow

- Two long-lived branches: `main` (stable, release) and `dev` (integration). Both
  are meant to be protected on GitHub (no direct pushes, pull request required);
  see `docs/branching.md` for the exact settings and why the initial scaffold
  commits are the one documented exception, being the repo's original setup before
  this rule existed.
- One feature branch per use case, branched from `dev`: `feat/<usecase-slug>` (for
  example `feat/register-account`, `feat/product-search`). Bug fixes use
  `fix/<slug>`, chores use `chore/<slug>`, docs-only work uses `docs/<slug>`.
- Open a pull request from the feature branch into `dev`. Never push a feature
  branch straight into `dev` or `main`.
- `dev` merges into `main` via its own pull request when a set of features is
  ready to release. `main` never receives a feature branch directly.
- Split commits by feature and task, the way this scaffold was built: one entity,
  one configuration, one behavior per commit. Never combine unrelated changes into
  one commit.
- Every commit ends with the attribution line given by the session's system
  reminder at commit time.

## Course Submission Requirements (GitHub management)

Full detail and current compliance status: `docs/submission.md`. Summary, all
required by the course brief:

- Repository must be named `ASPNET-<malop>-<hotenkhongdau>-<shortname>`.
  Done: `ASPNET-VX24TTK7-LeBinh-applestore`, see `docs/submission.md`.
- The instructor (`antonio86doan@gmail.com`) must be a repo collaborator;
  invited 2026-09-22, pending acceptance, see `docs/submission.md`.
- Root `README.md` must be kept continuously current, with full team contact
  info, through the life of the project, not written once and left stale.
- At least one commit per week, matching the work actually done that week.
  Commit history is graded directly.
- `progress-report/` (required) gets one file per week, and per the brief a
  report only counts if that week's commit history also shows activity.
- Repo root follows the required directory tree: `setup/`, `scr/`,
  `progress-report/`, `thesis/{doc,pdf,html,abs,refs,soft,docker}`: see
  `docs/submission.md` section 4.3 for what belongs in each.

When any of the above is still open, say so plainly rather than reporting the
task as done; these are graded requirements, not optional polish.

## Verify Before Delivering

- Never present a change as done without running `dotnet build && dotnet test` and
  showing the result.
- If a UI change cannot be verified through a running browser session in the
  current session, say so explicitly and state what still needs manual or
  Playwright verification. "It should work" is not delivery.

## Known Gaps (see docs/data-model.md for full detail)

- `Order.OrderStatus` 5th value resolved 2026-10-05 as `Confirmed` (between
  `Pending` and `Shipping`), see `docs/data-model.md`. Do not add further values
  without asking.
- Payment method and a few other enum ordinals were assigned during scaffolding
  because the report names the values but never numbers them; these are safe to
  treat as fixed, but are noted in `docs/data-model.md` for transparency.
