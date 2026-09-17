# Apple Store

ASP.NET Core MVC web application selling Apple technology products. Course project,
HCMUTE, Cong nghe phan mem, Nhom 05, 2025. Source requirements live in
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

- One feature branch per use case: `feat/<usecase-slug>` (for example
  `feat/register-account`, `feat/product-search`). Never commit feature work
  directly to `main` once real feature sessions start; this scaffold session is the
  documented exception, being the repo's initial setup.
- Split commits by feature and task, the way this scaffold was built: one entity,
  one configuration, one behavior per commit. Never combine unrelated changes into
  one commit.
- Every commit ends with the attribution line given by the session's system
  reminder at commit time.

## Verify Before Delivering

- Never present a change as done without running `dotnet build && dotnet test` and
  showing the result.
- If a UI change cannot be verified through a running browser session in the
  current session, say so explicitly and state what still needs manual or
  Playwright verification. "It should work" is not delivery.

## Known Gaps (see docs/data-model.md for full detail)

- `Order.OrderStatus` in the source report has an unresolved 5th value slot. Do not
  add a value to the `OrderStatus` enum without the team resolving this first.
- Payment method and a few other enum ordinals were assigned during scaffolding
  because the report names the values but never numbers them; these are safe to
  treat as fixed, but are noted in `docs/data-model.md` for transparency.
