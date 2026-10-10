# Verification log

Durable record of what was actually run and observed, so "it should work" never
substitutes for proof. Newest entry first. Append a new dated entry per
verification pass; do not edit or delete old ones, they are the audit trail.

## 2026-10-10: in-person sale and batch invoices (`feat/in-store-sale`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 633 of 633 (33 new) |
| `python3 setup/verify-sales/verify.py` | **11 of 11**, order, payment, stock and revenue compared with the database |
| related checks rerun | admin 31, orders 19, checkout 25, payment 16, reports 16, reviews 9, account 69, stock 13 |

**Refactor first:** the voucher check, the conditional stock take and the
voucher use moved from `CheckoutService` to `SaleRules` (no behavior change;
removing the stock or limit condition is still caught).

**Mutation checks** (all caught): no total check; any payment method;
a stock refusal ignored; an off-sale product sold; an unknown email sold as a
walk-in; the order not completed; the walk-in email read with `FirstAsync`
(the order and invoice pages crashed); the page for admins only; the check
that the sale is what was priced; a refused voucher still sellable.

**Found while building:** making `Orders.UserId` nullable broke the order
and invoice pages for a walk-in sale (`FirstAsync` on the customer's email);
a web test caught it before any commit.

**Review (code-reviewer agent).** No blocking findings. Fixed: a save failure
other than the same form twice became a bare 500 with the cause lost (stock
receipts had the same, both fixed); "Complete sale" was offered with a
refused voucher; a line switched after pricing to another product at the
same price could be sold unseen (now `QuotedFor`); the form-key steps were
copied in two controllers (now `FormKeys`). Tests added: a voucher used up
mid-sale, a counter sale cannot be cancelled or paid again. Not changed:
batch invoices run a few queries per order (no cap was asked for).

## 2026-10-10: stock intake (`feat/stock-intake`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 600 of 600 (29 new) |
| `python3 setup/verify-stock/verify.py` | **13 of 13**, stock and receipt lines compared with the database |
| related checks rerun | admin 31, checkout 25, orders 19, admin-catalog 15, prices 11, promotions 15 |

**Mutation checks** (all caught): opening read before the add; stock
overwritten instead of added (a sale lost); no duplicate check; no cost
ceiling; no overflow check; both double-save guards removed; a dotted cost
allowed; empty rows kept; the pages for admins only.

**Found by the live check:** after saving, the browser's Back fetched the
form again with a fresh key, and saving again received the goods twice
(stock 60 became 70). The key now lives in the form's address; Back returns
to it and a used key shows the saved receipt (RED then GREEN).

**Same class elsewhere:** checkout (cart empty), products and vouchers (name,
SKU, code unique) are safe after Back; a promotion is duplicated harmlessly;
a batch price change applies again (open question to the owner).

**Review (code-reviewer agent).** No blocking findings. Fixed: a duplicated
tab's goods looked received (now told to start a new receipt), an empty key
was accepted, quantity totals could overflow int, lines now go in variant
order (no SQL Server deadlock), one owner for the variant option text. Not
tested: the race past the early key check (shared in-memory connection).

## 2026-10-10: placeholder of the product's kind (`feat/photo-placeholder`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 571 of 571 (5 new) |
| live look at the five real products without a photo | watch, tablet and accessory drawings on the category row, the model page cards and search; screenshots checked at 1440px |

## 2026-10-09: batch prices and price history (`feat/price-history`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 566 of 566 (31 new) |
| `python3 setup/verify-prices/verify.py` | **11 of 11**, every price and log row compared with the database |
| all other live checks, rerun | account 69, admin 31, cart 23, checkout 25, payment 16, orders 19, admin-catalog 15, reports 14, reviews 9, compare 12, catalog-filter 16, promotions 15 |

**Mutation checks** (all caught): the batch without compare-and-swap; no
rounding; a price of 0 allowed; no version move (a stale edit page could then
save the old price back); the category ignored; a log row on every save; the
wrong old price logged; amounts with a dot allowed; no staff id; the page for
admins only; the mode check; the price ceiling.

**Found while testing:** "1.000" typed as an amount binds as 1, which would
have added one dong instead of a thousand. Amounts must now be whole numbers
(C# keeps "1.000" with three decimals, so it can be told apart). The class
of bug is the one already listed for vouchers.

**Review (code-reviewer agent).** No blocking findings; the log cannot
disagree with the saved price, because only the two price writers change a
price and both move the version. Fixed from it: a huge number crashed the page
or stored a price beyond decimal(12,2) (now refused everywhere through
`PriceLimits`); a forged mode value; the staff id read two ways (now one
extension). Added a test that a sale during a batch neither stops it nor
loses its stock. The history page lists every change, with no cap (none was
asked for).

**Oddities.** A full-page desktop screenshot showed the page repeated:
Playwright stitches full-page shots badly with the fixed background; the
script now takes a normal shot. The warm build server's Razor errors came
back after a batch of live checks; `dotnet build-server shutdown` cleared them.

## 2026-10-09: promotions (`feat/promotions`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 535 of 535 (43 new) |
| `python3 setup/verify-promotions/verify.py` | **15 of 15** |
| all other live checks, rerun | account 69, admin 31, cart 23, checkout 25, payment 16, orders 19, admin-catalog 15, reports 14, reviews 9, compare 12, catalog-filter 16 |

**Mutation checks** (all caught in the end): a free price allowed; the
window's end exclusive; code vouchers counted as promotions; the smallest
discount winning; a promotion's product list ignored; the cart charging the
own price; bands using the own price; each page reaching the other kind
(get, update, delete); 100% allowed; promotions page for admins only; a
promotion keeping a posted code or limits; the kind rules record changed.
One survived first: "a promotion keeps no minimum or limit" was enforced in
both the form and the service, so breaking the service changed nothing. The
form's copy was removed and the service owns the rule.

**Found by the live check:** the cart printed `@if (line.WasUnitPrice ...`
as text, because Razor reads `each@if` as an email-like word. The web test
had passed since the `<s>` tag inside still rendered; it now also checks no
`@if` reaches the page (RED then GREEN). The live check's own first runs
failed twice on its own logic (one colour sold in two regions at two prices;
the page reloaded to its default colour before buying).

**Found by the account check:** it copies the developer's database, which
did not have the new columns, so product pages gave a 500. The script now
migrates its copy first; the step is in `HANDOFF.md`.

**Review (code-reviewer agent).** No blocking findings. Fixed from it:
checkout looked a typed code up in every row (now code vouchers only, RED
then GREEN); the kind had defaults (now required); four scattered kind checks
(now `VoucherKindRules`). Tests added for a promotion ending between page and
press, stored zero or negative values, and an employee posting an edit to a
voucher's id. Kept as known limits: see `docs/finish-plan.md`.

**Build server.** The Razor errors in untouched views came back during
mutation runs, after several incremental builds in a row. Not reproducible
on demand (incremental, project-only and `dotnet ef` builds were all clean
when tried); `dotnet build-server shutdown` before each run avoids it.

## 2026-10-09: filter by price, sort by newest (`feat/price-filter`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 492 of 492 (20 new) |
| `python3 setup/verify-catalog-filter/verify.py` | **16 of 16** |
| related checks rerun | compare 12, cart 23, account 69 |

The live check compares each band with the database itself (52 products on
sale: 11, 14, 25 and 15 per band, a product can sit in two) and checks every
card price is inside its band.

**Mutation checks:** band not dropping products (9 tests failed), the upper
bound made inclusive (1), no Id tie-break for Newest (1), category landing
ignoring the band (2): all caught. A fifth, removing the controller's check of
unknown bands, survived: MVC's enum binder already refuses undefined values,
so the check was dead code and was removed.

**Decimal on SQLite.** Prices are stored as text, so the tests use 6.490.000
against the 10 million bound: compared as text it would fall out of "Under 10
million". It stays in, so EF Core compares the numbers.

**Review (code-reviewer agent).** No blocking findings. Added from it: tests
for a band name in another case and as its number, and the empty-list message
now names price.

**Oddities chased.** The first desktop screenshot showed one card where the
page counted 15: it was taken while the window grew from 390px and the cards
were still fading in. A fresh load shows four per row; the script now reloads
before the shot. The warm build server's Razor errors came back twice more
after view edits; `dotnet build-server shutdown` clears them each time.

## 2026-10-08: compare products (`feat/compare`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 472 of 472 (26 new) |
| `python3 setup/verify-compare/verify.py` | **12 of 12** |
| earlier checks, rerun 2026-10-09 | account 69, admin 31, cart 23, checkout 25, payment 16, orders 19, admin-catalog 15, reports 14, reviews 9 |

**Mutation checks** (each caught): no cap when reading the list; a hidden
product still holding a slot; hidden products shown; adding without checking
the product exists; the nav reading the old cookie on the page that trims
it (this one first survived, because the cap hid it; a sharper test was
added). `FormTokenTests` now also walks a model page and `/Compare`.

**Review (code-reviewer agent).** No blocking findings. Fixed: the nav count
was the request's old cookie on the same page that trimmed the list, and a
hand-edited cookie could show any count (now the list this response wrote,
capped at 3). Added tests: database failure on the page and on add keeps the
list, `//evil` and `/\evil` return addresses. Kept as a known limit: two tabs
adding at once, the last response wins (see `docs/finish-plan.md`).

**Oddities chased.** Twice a warm build server reported Razor errors in views
the change did not touch (`Home/Index`, `Products/Index`, `Reports/Print`).
`dotnet build-server shutdown` cleared it and a fresh `--no-incremental` build
was clean both times, so it is the compiler server, not the code. The live
script's first two runs failed on its own SQL: SQLite stores prices as text, so
`min()` compared strings; the script now casts.

## 2026-10-08: product reviews (`feat/reviews`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 446 of 446 (20 new) |
| `python3 setup/verify-reviews/verify.py` | **9 of 9** |
| earlier checks | account 69, admin 31, cart 23, checkout 25, payment 16, orders 19, admin-catalog 15, reports 14 |

**Mutation checks** (each caught): an order not delivered counted as a
purchase; an edit unhiding a hidden review; the staff review page open to
every signed-in account. `FormTokenTests` now also walks `/Admin/Reviews` and
`/Admin/Reports`.

## 2026-10-08: sales report, Excel export, invoices (`feat/admin-reports`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 426 of 426 (16 new) |
| `python3 setup/verify-reports/verify.py` | **14 of 14** |
| earlier checks | account 69, admin 31, cart 23, checkout 25, payment 16, orders 19, admin-catalog 15 |

The live check downloads the .xlsx through the browser and opens it (three
sheets) and prints the invoice to a real PDF with Chromium, with the print
button hidden on paper. **Mutation checks** (each caught): cancelled paid
orders counted; days in UTC; the last day cut off; the customer invoice read
without the owner check; reports open to every signed-in account.

**Found during this pass:** the stat cards wrapped long amounts (fixed);
the live check's sheet reader missed the `x:` namespace prefix (script fix).

## 2026-10-08: whole-app senior review and its fixes (`fix/review-findings`)

A review pass in the corjl style: VERIFY gate, LIVE gate over every script,
then an L4-L6 audit by two reviewers (security and authorization;
correctness and concurrency) over the whole codebase. Each finding was
checked by reading the code or by a RED test before anything was changed.

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 410 of 410 (12 new) |
| live checks: account, admin, cart, checkout, payment, orders, admin-catalog | 69, 31, 23, 25, 16, 19, 15 |

**Clean in review:** authorization on every action, ownership of orders,
addresses, cart lines and payments, anti-forgery on every post, open
redirects, XSS sinks, overposting, payment signatures and replays, tracked
reads after bulk updates, transactions around multi-step writes.

**Fixed (each RED then GREEN):**
1. Blocking: an admin price typed as shown ("24.990.000") or unreadable was
   saved as empty ("Contact for price"). Admin posts now check `ModelState`.
2. The registration code could be guessed without limit; five wrong codes
   now end the attempt (counted atomically).
3. The simulated gateway was refused only in Production; now it runs only
   in Development.
4. An undefined role or payment method was accepted.
5. Renaming a used voucher made a later cancel give the use back to the
   wrong voucher; a used voucher keeps its code.
6. Taking a product off sale did not move its version.
7. A database failure while reading an order for its email gave an error
   page after the order was saved.

**Process slip:** one GREEN commit (`c5d1283`) was made while a test still
failed, because the command chain used `;`. It was fixed in the next commit
(`553ae47`); commits are now made only after the test run passes.

**Left open (owner's call or a number needed):** no rate limit on `/Track`,
the code forms and forgot-password; "1.000" in a voucher's discount box
reads as 1 under the server's culture; no unique index on SKU (two retired
variants share SKUs); some older pages (addresses, profile, opening
checkout) rely on the global error page for a database failure.

## 2026-10-08: admin products, vouchers, accounts and photos (`feat/admin-products`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 398 of 398 (63 new) |
| `python3 setup/verify-admin-catalog/verify.py` | **15 of 15** |
| earlier checks: admin, account, cart, checkout, payment, orders | 31, 69, 23, 25, 16, 19 |

**Mutation checks** (each caught): product version ignored; the seen stock
ignored on a variant save; any photo address accepted; `RowVersion.Next`
returning the clock (stale edit in one tick); stamp checked every 30
minutes; vouchers open to employees; a stale product page reported as saved.

**Found during this pass:**
1. Using `UpdatedAt` as the version let a stale edit through when two saves
   fell in one clock tick (the voucher test, with a fixed clock). Fixed with
   `RowVersion.Next` for vouchers, products and variants.
2. Every admin save was a 400 in a real browser: the form tag helper leaves
   out the anti-forgery token when the action attribute is hand-written. The
   web tests had passed because they take the token from any form on the
   page. `FormTokenTests` now checks each post form on 21 pages; the forms
   ask for the token explicitly.
3. Photos: a research pass on Wikimedia Commons found exact-model photos for
   3 of the 8 products without one (checked by category, description, EXIF
   date and by eye); 4 have only the previous model, 1 has nothing.

**Not checked:** a real second admin in parallel (staged by changing the row
under the page instead).

## 2026-10-08: orders after checkout, tracking, staff pages (`feat/orders`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 335 of 335 (62 new: 36 `OrderManagementServiceTests`, 2 cancelled-order payment tests, 4 `OrderNotifierTests`, 20 order web tests) |
| `python3 setup/verify-orders/verify.py` | **19 of 19** |
| earlier checks: admin, account, cart, checkout, payment | 31, 69, 23, 25, 16 (the admin check now expects the Orders link) |

**Staged race:** staff confirm an order just before a customer's cancel
writes (`SqlRace` runs the confirm before the cancel's `UPDATE`). The
cancel finds the status moved and gives nothing back. Without the
compare-and-swap the test fails.

**Mutation checks** (each caught): no compare-and-swap; online order
confirmed unpaid; voucher use count allowed below zero; cash not recorded on
completion; tracking phone compared as typed; open attempts left payable
after a cancel; employees shut out of the order pages; the customer's cancel
button always shown; no order email; no shipped email.

**Found during this pass:**
1. A cancelled order was still offered "Pay now", and a late gateway success
   marked it paid. Now refused at start, and money arriving after a cancel is
   recorded with the order left cancelled and a refund due (RED then GREEN).
2. The "database refused this" check was copied in four controllers; it is
   one helper now.
3. `/Track` was reachable only from the shipping email; the footer links it.

**Not checked:** an employee made through a page (there is none; the live
check sets the role in its throwaway database); real email delivery for the
order emails (the live check reads them from the Development log).

## 2026-10-08: online payment through the simulated gateway (`feat/payment`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 273 of 273 (46 new: 12 `PaymentSignatureTests`, 19 `PaymentServiceTests`, 15 payment web tests) |
| `python3 setup/verify-payment/verify.py` | **16 of 16** |
| `python3 setup/verify-checkout/verify.py` | 25 of 25 |

**Mutation checks** (each caught): signature field signed with the rest;
attempts read tracked; amount not checked; success only from pending;
order marked paid without its condition; a failure overwriting a success;
a cash-on-delivery payment accepted; the owner check removed; Production
allowed with the simulator; the simulator not re-checking its form; the
simulator ignoring the mode.

**Found during this pass:**
1. A failed attempt was reused by "Pay now": the attempt was read as a
   tracked entity, which kept its status from before an `ExecuteUpdate`.
   Now read untracked; the retry test caught it.
2. The first version decided the gateway while registering services in
   `Program.cs`, where the test host's settings do not reach, so the
   "Production refuses" test could not fail. Moved to the options pattern
   with `ValidateOnStart`, which runs after every configuration source.
3. The shopper's return after the gateway's own call read "already paid";
   it now reads "Payment received".
4. One web test (another user's order) passed in RED only because the route
   did not exist; after GREEN a mutation of the owner check proves it.

**Not checked:** a real VNPay or MoMo sandbox (none available); an unpaid
online order holding stock (no expiry yet).

## 2026-10-07: checkout and vouchers, use cases 15-16 (`feat/checkout`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 227 of 227 (43 new: 28 `CheckoutServiceTests`, 15 in `CheckoutFlowTests` and `CheckoutFailureTests`) |
| `python3 setup/verify-checkout/verify.py` | **25 of 25** |
| `python3 setup/verify-cart/verify.py` | 23 of 23 |
| `python3 setup/verify-account/verify.py` (after the shared delivery fields) | 69 of 69 |
| `dotnet ef database update` on the dev database (backed up first) | `AddVouchers` applied, 3 demo vouchers present |

`CheckoutServiceTests` use the real `CartService` and SQLite. A competing
request is staged by a command interceptor that runs its SQL just before
the service's stock, voucher or cart write, on the same transaction.

**Mutation checks** (each caught by one test): stock condition removed;
voucher-use condition removed; cart-changed check removed; expected-total
check removed; rounding changed to banker's; anti-forgery removed from the
checkout post; the page keeping the posted total instead of the new one.

**Found during this pass:**
1. Three web tests placed orders for a user with no saved address and were
   stopped by validation (street address required), which is the intended
   behavior; they now type an address.
2. A `ModelState.Remove("ExpectedTotal")` in the controller did nothing (the
   input used another key) and a mutation of it survived; the line was
   removed and the hidden input rendered directly, and the mutation on the
   line that matters is caught.
3. The 390px order screenshot first showed the page twice; it was taken in
   the middle of a resize. After a reload the page is correct, and the
   check now reloads before measuring.

**Not checked:** two requests truly in parallel on the live server (staged
in unit tests instead); cancelling an order (task 8); the order email
(task 8).

## 2026-10-07: cart, use cases 11-14 (`feat/cart`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 184 of 184 (46 new: 26 `CartServiceTests`, 20 in `CartFlowTests` and `CartFailureTests`) |
| `python3 setup/verify-cart/verify.py` | **23 of 23** |
| `python3 setup/verify-account/verify.py` | 69 of 69 (nav now starts with the cart link) |
| `python3 setup/verify-admin/verify.py` | 31 of 31 (same) |
| `dotnet ef database update` on the dev database (backed up first) | `UniqueCartPerUserAndLinePerVariant` applied, both indexes present |

`CartServiceTests` run on a real SQLite schema. Races are staged with a
`SaveChangesInterceptor` that lets a second context write just before the
service's own save, so the unique indexes reject it as they would in
production.

**Attack sequences and mutation checks:**

| Sequence | Result |
|---|---|
| Another request adds the same line, or creates the cart, first | one cart, one line holding both quantities |
| Another request takes the last unit first | refused with "0 more", line unchanged |
| Retry removed | 3 tests fail |
| Stock condition removed from the `UPDATE` | 2 tests fail |
| Owner condition removed from change and remove | 2 tests fail |
| `[ValidateAntiForgeryToken]` removed from Add | 1 test fails |
| Return address not checked as local | 1 test fails |
| Write lock held on the live database while adding | message on the same product page, logged; the next click works |

**Found during this pass:**
1. `ProductVariants.SKU` is not unique (retired AirTag variants share SKUs
   with active ones), so the form posts the variant id and `VariantChoice`
   carries it.
2. Two web tests first failed on test data: a configuration named "256GB"
   (real ones include the product name) and comparing raw HTML where Razor
   encodes "Đ". Both were test fixes, not app fixes.
3. Adding the cart link to the nav changed the exact nav text that the
   account and admin checks compare; their expectations were updated.

**Not checked:** two requests truly in parallel on the live server (SQLite
serializes writes; the race path is covered by the staged unit tests);
checkout, which is task 6.

## 2026-10-07: Admin area and seeded admin (`feat/admin-area`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 138 of 138 (26 new: 14 `AdminSeederTests`, 2 `AdminSeedingStartupTests`, 10 `AdminAreaTests`) |
| `python3 setup/verify-admin/verify.py` | **31 of 31** |
| `python3 setup/verify-account/verify.py` (after moving its harness to `setup/verifylib.py`) | **69 of 69** |

The seeder tests run the real `UserManager`, `UserStore` and SQLite schema,
so Identity's own password rule and unique-email rule decide. The area
tests go through the real cookie handler and `[Authorize(Roles)]`.

**Attack sequences checked:**

| Sequence | How | Result |
|---|---|---|
| Owner's user-secrets or environment seed an admin into the tests | full suite run with `SEEDADMIN__EMAIL` and `SEEDADMIN__PASSWORD` set | 138 of 138 on the final commit; with the factory's blanking removed, `The_default_test_host_seeds_no_admin` fails (mutation check) |
| Seeder runs inside `dotnet ef database update` | migrate a fresh database with the seed variables set | 0 users afterwards, no seeder log |
| Seeder runs twice, or the settings change later | unit test, and the live check restarts with another password | one admin, first password still works, nothing logged |
| Seed email already belongs to a customer | unit test | refused, customer not promoted, password unchanged |
| Database never migrated | unit test, and live start in Production on an empty file | error names `dotnet ef database update`; the app still answers 200 |
| Weak password | unit test, live start with `q7Z` | warning with the 8-character reason; the password is not in the log; no user |
| `[Authorize]` without the role, or the on-sale count without its filter | mutation checks | 3 and 1 tests fail |

**Live check** (`setup/verify-admin/verify.py`, fresh database from the
real migrations, 69 products): a visitor sees no Admin link and `/Admin`
sends them to sign in with `ReturnUrl=%2FAdmin`; signing in lands on the
dashboard; counts match SQLite (69 products, 52 on sale, 0 customers, 0
orders); after a customer registers through the real form the count is 1;
no sideways scroll at 1440px or 390px; the Admin link works from the phone
menu; a customer gets the access denied page with a link home; after
signing out `/Admin` asks to sign in again. No console errors.

**Found during this pass:**
1. The first dashboard test expected seeded products, but `EnsureCreated`
   does not load the catalog (migrations do). The test now adds its own
   products, one of them hidden, so the on-sale filter is actually tested.
2. Self-review: a blank `SeedAdmin:FullName` would leave a blank name link
   in the nav. It now falls back to "Administrator" (RED and GREEN commits).
3. One live check first failed because it matched the word "created" in
   SQL column names and the existing HTTPS-redirect warning. It now looks
   only for log lines from `AdminSeeder`.

**Not checked:** two app instances seeding at the same moment (the unique
email index would refuse the second; that path through `UserStore` is not
tested); a role change while signed in (there is no page that changes roles
yet).

## 2026-10-05: real email over SMTP (`feat/smtp-email`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 112 of 112 (10 new: 8 `EmailTests`, 2 in `AccountSettingsFlowTests`) |
| `python3 setup/verify-account/verify.py` | **69 of 69** |

`EmailTests` use real collaborators: the sender choice is resolved from a
real `ServiceCollection` and configuration, and the failure test opens a
real socket to a closed local port (refused at once, well under the 30
seconds the test allows). The two new live checks start the app with SMTP
pointed at that closed port: Register shows "We could not send the email.
Please try again in a moment.", keeps the typed email, and the server log
has the failure.

**Found during this pass:** the GREEN commit went in before
`dotnet format` was checked (the command chain did not stop on the format
error); a separate style commit fixed the one initializer. Also: once
user-secrets hold SMTP settings, a Development run would send real mail and
the live check could no longer read codes from the log; the script now sets
`Smtp__Host` to empty.

### Real send to a real inbox (same day)

The owner stored the Gmail settings in user-secrets (sending account
kbsaigonese@gmail.com, App Password typed by the owner). The app ran in
Development against a temp copy of the database and the Register form was
submitted in Chromium.

| Run | Result |
|---|---|
| 1. revocation check on (default) | refused in the TLS handshake: "An incomplete certificate revocation check occurred"; the page showed "We could not send the email..." and the log had the error |
| 2. same, outside the session sandbox | same error, so it is .NET on macOS, not this session's network |
| 3. `Smtp__CheckCertificateRevocation=false` for that run | sent to kbsaigonese@gmail.com in 5.4 s, page moved to the code step, no error logged |
| 4. same settings, to lebinh030199@gmail.com | sent in 3.4 s; **the owner confirmed the email arrived** |

Runs 1 and 2 also showed the failure path working for real: a visible
message, the typed email kept, the failure in the log. The code from the
email was not typed back into the app in this pass; that step is covered by
the live check script with log-only mail.

## 2026-10-05: use cases 4-6, forgot and change password, profile, addresses (`feat/account-password-profile`)

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 102 of 102 (27 new: 9 `PasswordResetServiceTests`, 6 `ProfileServiceTests`, 12 `AccountSettingsFlowTests` through the real app) |
| `python3 setup/verify-account/verify.py` | **67 of 67** live browser checks (the 31 from the first entry plus 36 new) |

New live checks: login links to forgot password; unknown email is told so
with a register link; wrong reset code stays; right code signs in with
"Your password was reset."; afterwards the old password fails and the new
works; change password refuses a wrong current password, keeps the session,
and the changed password signs in; profile refuses another account's phone
and shows the new name in the nav; first address is the default, ticking
default moves it, "Make default" moves it back, edit and delete work;
another user's address is 404 and its row is untouched; Account, Profile,
ChangePassword, Addresses, Addresses/Create, ForgotPassword, ResetPassword
load at 1440px and 390px with no sideways scroll and no console errors; on a
phone the menu button reveals the name and sign out.

**Found and fixed during this pass:**
1. Address defaults were first cleared with `ExecuteUpdate`, which left the
   tracked rows stale, so a later "make default" in the same context was not
   saved. Caught by `ProfileServiceTests`; defaults are now set on tracked
   rows and saved once.
2. "Add an address" (a link styled as a button) was underlined; seen in the
   screenshot, fixed in CSS.
3. The first live run timed out signing out at 390px: the sign-out button is
   inside the collapsed menu, as designed. The script now opens the menu
   button first, which also checks that phone users can reach it.

**Not checked:** that other sessions end after a password change. Identity
checks the stamp every 30 minutes by default, so a live check would have to
wait that long; the unit test checks that the stamp changes.

## 2026-10-05: M1 account pages on ASP.NET Core Identity (`feat/identity-login`)

**Scope checked:** build, format, full test suite, migration on the real dev
database, and a live browser pass over every account page and path.

### Build, format, test, migration

| Command | Result |
|---|---|
| `dotnet build --no-incremental` | 0 warnings, 0 errors (a full rebuild found one CS8625 warning that incremental builds had hidden; fixed in its own commit) |
| `dotnet format --verify-no-changes` | clean |
| `dotnet test` | 75 of 75 pass (53 existing, 22 new: 7 `UserStoreTests`, 4 new `RegistrationServiceTests`, 11 `AccountFlowTests` running the real app through `WebApplicationFactory`) |
| `dotnet ef database update` on `src/AppleStore.Web/AppleStore.db` | applied `AddIdentityColumnsToUsers`, 4 new columns present |

### Live browser check

Rerunnable script: `python3 setup/verify-account/verify.py`. It starts the app
against a temp copy of the dev database (the dev database is not written to),
drives Chromium with `playwright-cli`, reads each OTP from the app log, and
prints one line per check. Result: **31 of 31 checks passed.**

| Area | Checks |
|---|---|
| Pages | Login, Register, AccessDenied: HTTP 200 at 1440px and 390px, no sideways scroll, 0 console errors |
| Redirects | anonymous `/Account` goes to sign in with `ReturnUrl`; `/Account/VerifyOtp` with no registration goes to Register |
| Register | empty form and mismatched passwords stopped in the browser with no POST sent; short password shows the 8-character rule; same email in other case refused; same phone refused |
| Code page | wrong code refused; right code creates the user and signs them in; reusing a spent code says expired and offers Register again or sign in |
| Sign in | wrong password and unknown email show the identical message; upper-case email works; local return URL followed; external return URL lands on `/`; cookie is a session cookie without "remember me" and persistent (about 14 days) with it |
| Sign out | GET `/Account/Logout` is 404 and keeps the session; POST without anti-forgery token is 400; the nav button signs out |
| Lockout | right password after 5 wrong ones is refused with the lockout message; the row has `LockoutEnd` set, upper-case `NormalizedEmail`, a 32-character `SecurityStamp` |
| Server error | app in Production against an empty database: sign in ends on "Something went wrong" with a "Back to home" link, not a hang |

**Observed and explained, not bugs:**
- After lockout the row shows `AccessFailedCount = 0`. Identity resets the
  count when it sets `LockoutEnd`; the lock itself is `LockoutEnd`.
- The Production error page renders without CSS. That is the
  run-from-source Production gotcha already in `docs/architecture.md`
  ("Running locally"): static assets are only served from the Development
  manifest or a published build. It predates this branch. The page also
  still carries the template's "Development mode" paragraph, also
  pre-existing (`Views/Shared/Error.cshtml` is not touched here).

### Found and fixed during this pass

1. Two pending registrations for one email: confirming the second crashed
   with `DbUpdateException` (HTTP 500) on the base branch. Now it returns
   "already exists". Covered by `RegistrationServiceTests`.
2. Opening the code page without a registration showed a form that could
   only fail with no visible message. Now it redirects to Register.
   Covered by `AccountFlowTests`.

### Not checked in this pass

- Forgot password, change password, update profile: not built yet.
- No admin account exists yet, so `[Authorize(Roles = "Admin")]` has no
  page to check; that comes with M7.

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
