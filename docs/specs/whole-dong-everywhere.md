# Whole dong everywhere (`fix/whole-dong-everywhere`)

Status: **PLAN draft, not approved, no code yet** (2026-10-12). The next step
is the owner's yes on the approach and answers to the questions below. Only
then RED tests.

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

## Questions for the owner

1. Scope: only staff-typed money (table above), or also existing data
   (a one-off check or migration that rounds stored fractions)?
2. A fixed voucher typed as `50.000`: today it binds as 50 with scale 3.
   Refuse it like the prices form does (current house rule), or accept dots
   as thousands separators everywhere?
3. Does the batch percent rule (round to 1.000 dong) stay as is?

## Test plan (after approval)

- RED per form: a fraction or a dotted amount is refused with the field
  message, nothing is saved; a whole amount saves.
- Run against the real controllers through `AppleStoreWebFactory`, next to
  `AdminCatalogFlowTests` and the voucher and promotion flow tests, no
  mocked validation.
- Live check: extend `setup/verify-admin-catalog` (product and variant
  prices) and `setup/verify-promotions` (vouchers and promotions) with one
  refused and one accepted amount per form at 1440px and 390px.
