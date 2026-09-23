# Apple Store

Website ban san pham cong nghe cua Apple. Do an mon hoc Cong nghe phan mem, TVU,
2025, individual project.

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

## Repository organization

This repo also follows the course's required top-level directory tree (see
`docs/submission.md` for the full requirement and rationale):

```
setup/            install/run instructions, test data matching the demoed result
scr/              required folder name per the brief; actual source is in src/, see scr/README.md
progress-report/  weekly progress reports (one file per week)
thesis/           project documents: doc/, pdf/, html/, abs/, refs/ (soft/, docker/ if used)
```

## Contact

Individual project, one author:

- Le Binh, kbsaigonese@gmail.com

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
