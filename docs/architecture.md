# Architecture

## Tech stack

| Layer | Choice |
|---|---|
| Web framework | ASP.NET Core MVC, Razor views |
| ORM | Entity Framework Core, Code First |
| Database, local dev | SQLite (`AppleStore.db`, gitignored) |
| Database, schema shape | SQL Server types (`nvarchar`, `decimal(12,2)`, `datetime2(0)`), see below |
| Tests | xUnit |
| Migrations | `dotnet-ef`, installed as a local tool via `dotnet-tools.json` |
| .NET | 10 (LTS) |

## Project layout

```
src/AppleStore.Domain/          entities and enums, zero external dependencies
src/AppleStore.Infrastructure/  AppDbContext, IEntityTypeConfiguration<T>, migrations,
                                 Services/ (business logic: OtpService,
                                 RegistrationService, IEmailSender)
src/AppleStore.Web/             controllers, views, wwwroot, Program.cs
tests/AppleStore.Tests/         xUnit
```

Dependency direction: `Web -> Infrastructure -> Domain`, and `Web -> Domain`
directly for anything that only needs entity/enum types (view models, mapping).
`Domain` never references EF Core or ASP.NET Core.

Why this split: `Domain` is what every layer including future tests can trust
without pulling in a database or a web server. `Infrastructure` is the only place
that knows about EF Core, so switching ORMs or database providers stays contained
there. `Web` is the only project with UI or HTTP concerns.

**Business services live in `AppleStore.Infrastructure/Services/`, not a separate
`Application` project.** This was a real decision, made when the first service
(`RegistrationService`) needed a home: a fifth project would have contradicted
the three-layer split above for the sake of a rule (never let a service touch EF
Core directly) this project's size does not need yet. Services take `AppDbContext`
directly as a constructor dependency rather than going through a repository
abstraction, for the same reason. Revisit this if services grow enough that
Infrastructure becomes a dumping ground; nothing about the current layout blocks
extracting an `Application` project later.

## Why SQLite now

This Mac has no Docker/Colima/Podman installed, and SQL Server has no native macOS
build, so running actual SQL Server locally would mean installing Docker Desktop
first, which needs manual, interactive first-run setup that cannot be scripted.
SQLite needs no server process, no account, and no license, and EF Core supports it
as a first-class provider. Every column constraint the report specifies
(`HasMaxLength`, `HasPrecision(12, 2)` on money columns) is configured on the
`IEntityTypeConfiguration<T>` classes, which are provider-agnostic: EF Core applies
them regardless of which provider is active.

## Switching to SQL Server later

When Docker Desktop (or an Azure SQL Database, which has a free tier) is available:

1. Add the `Microsoft.EntityFrameworkCore.SqlServer` package to
   `AppleStore.Infrastructure` and `AppleStore.Tests` alongside (or instead of)
   the Sqlite package.
2. In `Program.cs`, change `options.UseSqlite(...)` to
   `options.UseSqlServer(...)`.
3. Update the `Default` connection string in `appsettings.json` (and
   `appsettings.Development.json`) to a SQL Server connection string.
4. Delete `src/AppleStore.Infrastructure/Migrations/` and regenerate with
   `dotnet tool run dotnet-ef migrations add InitialCreate --project src/AppleStore.Infrastructure --startup-project src/AppleStore.Web`,
   since SQLite and SQL Server migrations are not interchangeable (different SQL
   dialect in the generated migration code), even though the entity configuration
   that drives them does not change at all.

No entity, enum, or configuration class changes are needed for this swap. That is
the point of keeping `HasMaxLength`/`HasPrecision` explicit instead of relying on
SQLite's default dynamic typing.

## Running locally

`dotnet run --project src/AppleStore.Web` from the repo root, or `dotnet run`
from inside `src/AppleStore.Web`, picks up `Properties/launchSettings.json`,
which sets `ASPNETCORE_ENVIRONMENT=Development`. Keep it that way: the scoped-CSS
bundle (`AppleStore.Web.styles.css`) that the default MVC layout references is
only served from the Development-time static web assets manifest, not physically
present in `wwwroot`, so overriding the launch profile (for example passing
`--no-launch-profile --urls ...` to pin a specific port) without also setting
`ASPNETCORE_ENVIRONMENT=Development` explicitly produces a working app with a
500 on every page's stylesheet request. See `docs/verification.md` for how this
was found.
