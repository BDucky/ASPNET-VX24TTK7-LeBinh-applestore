# Verification log

Durable record of what was actually run and observed, so "it should work" never
substitutes for proof. Newest entry first. Append a new dated entry per
verification pass; do not edit or delete old ones, they are the audit trail.

## 2026-09-17: scaffold plus M1 registration service

**Scope checked:** full solution build, full test suite, and a live browser
check of the running app (no custom UI exists yet, see below).

### Build and test

```
dotnet build
  Build succeeded.
  0 Warning(s)
  0 Error(s)

dotnet test
  Passed!  - Failed: 0, Passed: 11, Skipped: 0, Total: 11
```

The 11 tests: 1 schema smoke test (`SchemaSmokeTests`, all 24 entity
configurations create a schema cleanly against SQLite in-memory), 4
`OtpServiceTests`, 6 `RegistrationServiceTests`.

### Migration

`dotnet ef database update --project src/AppleStore.Infrastructure --startup-project src/AppleStore.Web`
against a real (non-in-memory) SQLite file: applied cleanly, all 24 tables and
their indexes created, `__EFMigrationsHistory` recorded the migration. Confirms
the migration is not just internally consistent (which the smoke test already
proves) but actually applies outside a test fixture.

### Live browser check

Launched `dotnet run` from `src/AppleStore.Web`, drove it with a headless
Chromium via Playwright (no `chromium-cli` or project run-skill was available on
this machine at the time; see the `run` skill's fallback pattern), hit the two
pages that exist:

| Page | HTTP status | Console errors |
|---|---|---|
| `/` (home) | 200 | 0 |
| `/Home/Privacy` | 200 | 0 |

**What was on screen:** the default ASP.NET Core MVC template pages
(`AppleStore.Web` nav bar, "Welcome" heading, unedited Privacy placeholder). This
is correct for the current state, not a gap in verification: only
`OtpService` and `RegistrationService` exist so far (see `docs/roadmap.md`),
neither of which has a controller or view yet.

**Found during this check, not a bug:** the first attempt launched with
`--no-launch-profile --urls http://localhost:5286` to pin a specific port, which
skips `launchSettings.json` and so never sets `ASPNETCORE_ENVIRONMENT=Development`.
The scoped-CSS bundle the default layout references only serves from the
Development-time static web assets manifest, so every page 500'd loading its
stylesheet. Re-ran with `ASPNETCORE_ENVIRONMENT=Development` set explicitly and
got the zero-error result above. A plain `dotnet run` (no `--no-launch-profile`
override) already sets this correctly. Documented in
`docs/architecture.md`, "Running locally," so the next person who needs a
specific port does not lose time on the same thing.

**Screenshots:** captured with a throwaway Playwright script, not committed to
the repo (they would go stale the moment real UI exists, and the repo has no
mechanism yet for keeping screenshot fixtures current). Published as a Claude
artifact for that session's review; that link is session-scoped, not a permanent
project asset, treat this written record as the source of truth going forward
rather than the link.

### Not checked in this pass

- No UI exists to click through beyond the two default pages above.
- Login, forgot password, change password, update profile: not built yet.
- E1-E5 error-path walk (network drop, backend error, slow response, unmount
  mid-flight, not-yet-loaded) from the global error-handling mandate does not
  apply yet either, there is no async UI surface to walk.
