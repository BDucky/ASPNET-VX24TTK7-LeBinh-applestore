# Whole dong everywhere (`fix/whole-dong-everywhere`)

Status: **done** (2026-10-12). The owner approved the proposed approach and
answered the three questions below with the recommended defaults. Code,
tests and the live check all landed; see "Outcome" at the bottom.

## Why

Two forms already refuse an amount that is not whole dong (`WholeDong.Is`,
`src/AppleStore.Web/Models/Admin/AdminForms.cs`): the batch price amount and
the stock unit cost. Every other money field accepts fractions. The
2026-10-09 promotions review recorded "a fixed discount may have fractions
of a dong, as vouchers already could" (`docs/finish-plan.md`, known gaps).
The branch name is the owner's; the exact scope below is Claude's reading of
it and needs confirming.

## Sweep (2026-10-12, Claude)

Money typed by staff:

| Site | Field | Whole-dong check today |
|---|---|---|
| Admin, Prices batch (`PricesController:37`) | `PriceBatchForm.Value` in amount mode | yes |
| Admin, Stock receive (`StockController:51`) | `StockLineForm.UnitCost` | yes |
| Admin, Products edit | `ProductForm.BasePrice` | **no** |
| Admin, Products, add variant | `VariantAddForm.Price` | **no** |
| Admin, Products, change variant | `VariantChangeForm.Price` | **no** |
| Admin, Vouchers and Promotions | `VoucherForm.Value` when type is fixed amount | **no** |
| Admin, Vouchers and Promotions | `VoucherForm.MinOrderAmount` | **no** |
| Admin, Sales | `ExpectedTotal` | not typed, server computed |

Money computed by the app:

| Site | Rule today |
|---|---|
| `DiscountMath.Amount` | percent rounds to whole dong, half away from zero; a fixed amount is used as stored |
| `PriceService` batch percent | rounds to 1.000 dong |
| `ReportModels.AverageOrder` | rounds to whole dong |
| `PriceText` | prints whole dong, so a stored fraction is hidden on screen but kept in totals |

Not checked yet: whether any seeded or existing row already holds a fraction
(`Vouchers.DiscountValue`, `ProductVariants.Price`, `Products.BasePrice`).

## Proposed approach (Claude's, for approval)

1. One owner: the existing `WholeDong` rule. The five unchecked fields use
   it, through a validation attribute on the form models (ASP.NET model
   validation) so each controller does not repeat an `if`. The two existing
   `AmountIsWhole` / `CostsAreWhole` checks move onto the same attribute.
2. A refused field shows a message on the field and keeps the form filled
   in, as the prices and stock forms do now.
3. Percent values (voucher percent, batch percent) are not money and keep
   their current rules.

## Questions for the owner (answered 2026-10-12)

1. Scope: only staff-typed money (table above), or also existing data
   (a one-off check or migration that rounds stored fractions)? **Owner:
   only staff-typed money.** No migration or data sweep was added.
2. A fixed voucher typed as `50.000`: today it binds as 50 with scale 3.
   Refuse it like the prices form does (current house rule), or accept dots
   as thousands separators everywhere? **Owner: refuse it, keep the house
   rule.**
3. Does the batch percent rule (round to 1.000 dong) stay as is? **Owner:
   yes, unchanged.**

## Outcome

- One `WholeDongAttribute` (`src/AppleStore.Web/Models/Admin/AdminForms.cs`)
  replaces `WholeDong.Is` plus the two ad-hoc `AmountIsWhole` /
  `CostsAreWhole` properties. Applied directly to `ProductForm.BasePrice`,
  `VariantAddForm.Price`, `VariantChangeForm.Price`, `VoucherForm.MinOrderAmount`,
  `StockLineForm.UnitCost`; gated by a sibling enum property (so the same box
  is left alone when it holds a percent, not money) on `VoucherForm.Value`
  (only when `Type == Fixed`) and `PriceBatchForm.Value` (only when
  `Mode == Amount`). `PricesController` and `StockController` collapsed back
  to a plain `ModelState.IsValid` check.
- Tests: `AdminCatalogFlowTests` gained cases for a one-dot base price, a new
  variant's price, a fixed voucher's value and its minimum order (each
  refused), a percent voucher typed with a dot (still saves, proving the
  gate reads the sibling field correctly), and a fixed voucher with whole
  amounts (saves). 644 tests, all green.
- Live check: extended `setup/verify-admin-catalog` for the product base
  price, a new variant's price, and a fixed voucher's value and minimum
  order (refused with a dot, saved whole); 20 of 20 checks pass. Did not
  touch `setup/verify-promotions`: `VoucherKindRules.For(Automatic)` sets
  `HasLimits: false`, so `MinOrderAmount` never reaches a promotion row, and
  `New`/`Edit` are the same `DiscountAdminController` action for both kinds,
  so the `Value` gate is already exercised through the voucher script.
  15 of 15 promotion checks still pass (no regression).
