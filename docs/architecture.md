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

## Authentication: ASP.NET Core Identity

Added 2026-10-05. Sign-up, sign-in, lockout and roles use ASP.NET Core
Identity (`UserManager<User>`, `SignInManager<User>`, the Identity
application cookie, `[Authorize]`), the same mechanism the course's sample
project uses, but over this project's own `Users` table:

| Piece | File | Role |
|---|---|---|
| `UserStore` | `Infrastructure/Identity/UserStore.cs` | Lets Identity read and write `Users`; the email is the user name |
| `AppUserClaimsPrincipalFactory` | `Infrastructure/Identity/` | Puts the role (`Customer`, `Employee`, `Admin`) and full name into the cookie |
| `AddAppleStoreIdentityCore` | `Infrastructure/Identity/IdentityServiceCollectionExtensions.cs` | The one place the password and lockout rules are set; the tests use it too |
| `AccountController` | `Web/Controllers/AccountController.cs` | Register, verify OTP, sign in, sign out, forgot and reset password, change password, profile, account page |
| `AddressesController` | `Web/Controllers/AddressesController.cs` | The signed-in user's delivery addresses at `/Account/Addresses` |
| `PasswordResetService` | `Infrastructure/Services/` | Reset code in `UserTokens`, then Identity's `ResetPasswordAsync` |
| `ProfileService` | `Infrastructure/Services/` | Name, phone and addresses, always scoped to the signed-in user |

Rules: password at least 8 characters, no other complexity rule; 5 wrong
passwords or wrong reset codes lock the account for 5 minutes (Identity's
defaults). Changing or resetting the password changes the security stamp;
other signed-in sessions end at their next stamp check, which Identity runs
every 30 minutes by default. `Infrastructure` references the ASP.NET Core
shared framework for Identity's token providers and data protection. `Domain`
still has no ASP.NET reference: `User` only gained plain columns.

## Admin area and the first admin

Added 2026-10-07. The admin pages live in an ASP.NET Core Area,
`Web/Areas/Admin/`, routed by `{area:exists}/{controller=Dashboard}/...`
ahead of the default route, so `/Admin` is the dashboard. Every admin
controller carries `[Area("Admin")]` and `[Authorize(Roles = "Admin")]`;
the role comes from the claim `AppUserClaimsPrincipalFactory` writes at
sign-in. A visitor is sent to sign in, any other role to
`/Account/AccessDenied`. The area uses the store's `_Layout` (no copy) plus
`Areas/Admin/Views/Shared/_AdminNav.cshtml`; the nav shows an Admin link
only to admins.

Nobody can register an admin. `AdminSeeder`
(`Infrastructure/Identity/AdminSeeder.cs`) runs once in `Program.cs` before
the app serves requests:

| Database | `SeedAdmin` settings | Outcome |
|---|---|---|
| has an admin | anything | nothing changes, nothing logged |
| no admin | email and password set | admin created through `UserManager` (same password policy), logged without the password |
| no admin | missing | warning naming the settings to set |
| no admin | password rejected, or email already a customer's | warning with Identity's reason; the customer is never promoted |
| no tables yet | anything | error telling to run `dotnet ef database update`; the app still starts |

`dotnet ef` builds the host but stops before code after `Build()`, so the
seeder does not run during migrations (checked live, see
`docs/verification.md`, 2026-10-07).

## Cart

Added 2026-10-07, use cases 11-14. A cart needs an account (owner's decision):
the product page shows a visitor a sign-in link that comes back to the same
colour and region, and `CartController` carries `[Authorize]`.

| Piece | File | Role |
|---|---|---|
| `CartService` | `Infrastructure/Services/CartService.cs` | add, change, remove, view, count; always scoped to the user's own cart |
| `CartController` | `Web/Controllers/CartController.cs` | `/Cart`, posts with anti-forgery; every outcome comes back as a message, a database failure included |
| `CartMessages` | `Web/Models/Cart/CartMessages.cs` | the one place a result becomes a sentence |
| `CartCountViewComponent` | `Web/ViewComponents/` | the cart link and item count in the nav |
| `_CartNotice` | `Web/Views/Shared/` | shown by `_Layout` on whatever page an action returns to |

Rules: one cart per user and one line per variant (unique indexes). Adding
again raises the quantity in one `UPDATE` that also checks the stock, so two
requests cannot both pass the check; a unique-index violation from a request
that got there first is retried once, and on that attempt the line exists.
Quantity stays between 1 and the variant's stock. The cart reserves nothing
and freezes no price: prices are read live, and checkout (task 6) checks
again. A line whose variant left sale, lost its price or its stock stays
visible with the reason and leaves the subtotal.

## Checkout and vouchers

Added 2026-10-07, use cases 15-16. `CheckoutService` prices the cart through
`CartService` (one rule for what can be bought), applies a voucher by
BM_VOUCHER_01 and places the order. `CheckoutController` (`/Checkout`) has one
form: "Apply" prices again and writes nothing, "Place order" validates the
`DeliveryFields` (shared with saved addresses) and places it. `/Orders/{id}`
shows the shopper's own order.

Placing is one EF Core transaction in which every write is conditional:

| Write | Condition | On a miss |
|---|---|---|
| `ProductVariants.StockQty -= q` | stock still at least q, still on sale | roll back, "no longer has enough stock" |
| `Vouchers.UsedCount += 1` | still active, a use still left | roll back, "fully used" |
| delete each cart line | same id, same quantity, same user | roll back, "your cart changed" |
| insert `Orders`, `OrderItems` (prices frozen), `Payments` (COD, pending) | | |

Before any write the total is compared with the one the page showed
(`ExpectedTotal`); a different total is shown again instead of being placed.
A second submit finds the cart empty. Voucher rules: both ends of the window
count, a percent discount rounds to whole dong half away from zero, no
discount exceeds the lines it applies to, shipping is free. The clock is
`TimeProvider`, so tests fix the time.

## Email

`IEmailSender` is the only thing the services call. `AddAppleStoreEmail`
(`Infrastructure/Services/EmailServiceCollectionExtensions.cs`) picks the
implementation once, at startup:

| `Smtp:Host` | Sender | Behavior |
|---|---|---|
| empty or missing | `DevEmailSender` | writes each email to the log; tests and `setup/verify-account/verify.py` run this way |
| set | `SmtpEmailSender` | MailKit over SMTP; port 465 uses TLS from the start, any other port must upgrade with STARTTLS; the "Smtp" section is bound with the options pattern and validated at startup, so a half-filled section stops the app at launch |

A failed send (server unreachable, wrong password) is logged and becomes
`EmailSendException`; registration and password reset turn it into "We could
not send the email. Please try again in a moment." on the same form, and
leave no usable attempt or code behind. The password goes in user-secrets
(see the root README), never in `appsettings.json`.
