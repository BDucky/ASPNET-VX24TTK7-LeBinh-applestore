# Apple Store

Website ban san pham cong nghe cua Apple. Do an mon hoc Cong nghe phan mem, HCMUTE,
Nhom 05, 2025.

An e-commerce web application for selling Apple technology products, built as a
software engineering course project.

## Stack

- ASP.NET Core MVC (Razor views)
- Entity Framework Core, Code-First
- SQLite for local development (schema is SQL Server shaped and provider-agnostic;
  see `docs/architecture.md` for how to switch)
- xUnit for tests

## Project layout

```
src/AppleStore.Domain/          entities and enums, no external dependencies
src/AppleStore.Infrastructure/  EF Core DbContext, entity configuration, migrations
src/AppleStore.Web/             controllers, views, wwwroot, Program.cs
tests/AppleStore.Tests/         xUnit tests
docs/                           requirements, data model, architecture, roadmap
```

## Running locally

```bash
dotnet tool restore
dotnet ef database update --project src/AppleStore.Infrastructure --startup-project src/AppleStore.Web
dotnet run --project src/AppleStore.Web
```

## Documentation

See `docs/requirements.md` for the source requirements, `docs/data-model.md` for the
full schema, `docs/architecture.md` for the project layout rationale, and
`docs/roadmap.md` for the proposed implementation sequencing.
