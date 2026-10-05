# Apple Store

Website ban san pham cong nghe cua Apple. Do an mon hoc Chuyên đề ASP.net, TVU,
2026, individual project.

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

- Le Binh, binhl030199@tvu-onschool.edu.vn

## Running locally

```bash
dotnet tool restore
dotnet ef database update --project src/AppleStore.Infrastructure --startup-project src/AppleStore.Web
dotnet run --project src/AppleStore.Web
```

Without further setup, emails (registration and password-reset codes) are
written to the console log, not sent. To send them for real through Gmail,
create an App Password for the sending Gmail account (Google Account,
Security, 2-Step Verification, App passwords), then store the settings in
user-secrets on your own machine:

```bash
dotnet user-secrets set "Smtp:Host" "smtp.gmail.com" --project src/AppleStore.Web
dotnet user-secrets set "Smtp:Port" "587" --project src/AppleStore.Web
dotnet user-secrets set "Smtp:UserName" "<sending gmail address>" --project src/AppleStore.Web
dotnet user-secrets set "Smtp:FromAddress" "<sending gmail address>" --project src/AppleStore.Web
dotnet user-secrets set "Smtp:Password" "<16-character app password>" --project src/AppleStore.Web
```

On macOS the TLS handshake with smtp.gmail.com can fail with "An incomplete
certificate revocation check occurred" (it did on the development machine).
If it does, turn that one check off for your machine; the certificate and
host name are still verified:

```bash
dotnet user-secrets set "Smtp:CheckCertificateRevocation" "false" --project src/AppleStore.Web
```

User-secrets live outside the repository and are read only in Development.
`dotnet user-secrets clear --project src/AppleStore.Web` goes back to
log-only mail.

## Documentation

See `docs/requirements.md` for the source requirements, `docs/data-model.md` for the
full schema, `docs/architecture.md` for the project layout rationale, and
`docs/roadmap.md` for the proposed implementation sequencing.
