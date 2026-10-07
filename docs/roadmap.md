# Implementation Roadmap

The source report specifies requirements and a data model; it does not prescribe a
delivery order. The milestones below are this scaffolding session's own proposed
sequencing of the 37 use cases from `docs/requirements.md`, grouped by what they
share technically. Treat this as a starting point to adjust, not a fixed plan.
Each milestone is sized to be its own set of feature branches and TDD sessions, not
one big session.

Order and status of the remaining work: `docs/finish-plan.md`.

## M1: Auth and OTP

Use cases 1-6 (register, send OTP, log in, forgot password, change password,
update profile). Entities already scaffolded: `User`, `UserToken`, `Address`.
This is the natural first milestone because almost every other use case requires
an authenticated user.

**Progress (see `HANDOFF.md` for the session-by-session detail):**

| Piece | Status |
|---|---|
| `OtpService` (generate, validate) | done, tested, merged path via PR #2 |
| `RegistrationService` (duplicate checks, pending state, OTP confirm, hashed password) | done, tested, PR #2 |
| `IEmailSender` / `DevEmailSender` | done, PR #2 |
| Register + verify-OTP controller and views | done, ASP.NET Core Identity, `feat/identity-login` |
| Log in, log out, lockout after 5 wrong passwords | done, `feat/identity-login` |
| Forgot password (code by email, `UserTokens`) | done, `feat/account-password-profile` |
| Change password | done, `feat/account-password-profile` |
| Update profile and delivery addresses | done, `feat/account-password-profile` |

The registration flow does not use `UserToken` the way the schema's
`UserTokenType.RegisterOtp` value implies. See `docs/data-model.md`,
"Registration OTP does not use UserTokens," for why and what that means for
`UserTokenType.RegisterOtp` going forward.

## M2: Catalog browse, search, filter, compare

Use cases 7-10. Entities: `Product`, `ProductVariant`, `Category`,
`CategoryAttribute`, `AttributeDefinition`, `ProductAttributeValue`, `OptionType`,
`OptionValue`, `VariantOption`, `ProductImage`, `Favorite`, `CompareList`,
`CompareItem`. See `docs/ux-research.md` for concrete listing/PDP/comparison
patterns from apple.com and Vietnamese resellers, including the open
variant-picker decision (separate pages per storage tier vs an in-page
selector) this milestone needs to settle explicitly.

## M3: Cart and checkout

Done so far (2026-10-07): the cart, use cases 11-14 (`docs/architecture.md`, "Cart").

Use cases 11-17 (cart CRUD, place order, apply voucher, pay). Entities: `Cart`,
`CartItem`, `Order`, `OrderItem`. Voucher itself has no dedicated table in the
report's schema (it is referenced by `Order.VoucherCode` as a string); deciding
whether vouchers need their own table, versus living in configuration, is a design
question for this milestone, not something the scaffold should have guessed at.

## M4: Payment integration

Part of use case 17, split out because it is its own integration surface: COD (no
external call), VNPay, and MoMo. Entity: `Payment`. This milestone should resolve
the `PaymentMethod` ordinal assignment's real-world mapping to each gateway's own
method codes, and needs sandbox credentials for VNPay/MoMo before it can start.

## M5: Order management, tracking, shipments

Use cases 18, 19, 22-24 (employee order processing, tracking assignment, status
updates, customer order lookup and tracking). Entity: `Shipment`. The `OrderStatus`
gap that blocked this milestone was resolved on 2026-10-05 (fifth value
`Confirmed`), see `docs/data-model.md`.

## M6: Employee stock, pricing, promotions

Use cases 20-21, 28 (stock intake, in-person sale, promotions). No new entities;
this is service and controller logic over `ProductVariant.StockQty` and `Product`.
The report's price-history log (mentioned in the functional requirements table's
formula for price updates) has no dedicated table in the schema chapter either;
same open design question as vouchers in M3.

## M7: Admin CRUD and reporting

Done so far (2026-10-07): the Admin area itself, `/Admin` behind the Admin
role, a dashboard with product, customer and order counts, and the first
admin account seeded from configuration (`docs/architecture.md`, "Admin
area and the first admin").

Use cases 25-27, 29-31, 33-36 (product/voucher CRUD, business and revenue reports,
export). No new entities beyond what M1-M6 already cover; this is read-heavy
aggregation queries and Excel/PDF export.

## M8: Reviews

Use cases 32, 37 (submit review, order confirmation email). Entities: `Review`,
`ReviewMedia`. Placed last because it depends on a completed order existing
(`PurchaseVerified` requires a real purchase to check against), and order
confirmation email depends on M3/M4 being done.
