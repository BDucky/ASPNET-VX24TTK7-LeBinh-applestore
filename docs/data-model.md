# Data Model

The full schema from the source report (Chapter 3, "Thiet ke du lieu"), as
implemented in `AppleStore.Domain` and mapped in
`AppleStore.Infrastructure/Data/Configurations`. Column names match the report
exactly. C# types are Code First; the SQL Server column types the report specified
(`nvarchar`, `decimal(12,2)`, `datetime2(0)`) are reproduced through
`HasMaxLength`/`HasPrecision` in each `IEntityTypeConfiguration<T>`, so they hold
even though local development runs on SQLite. See `docs/architecture.md` for how
that provider swap works.

## Tables

### Categories
`Id (PK)`, `Name (nvarchar 120)`, `Slug (nvarchar 140)`, `ParentId (FK -> Categories, nullable, self-referencing)`

### Attributes
Entity class: `AttributeDefinition` (renamed from the report's "Attributes" to
avoid colliding with `System.Attribute`; mapped back to table `Attributes` via
`ToTable`). `Id (PK)`, `Name (nvarchar 120)`, `DataType (int)`, `Unit (nvarchar 20, nullable)`

### OptionTypes
`Id (PK)`, `Code (nvarchar 30)`

### Users
`Id (PK)`, `Email (varchar 120, unique)`, `PasswordHash`, `FullName (nvarchar 120)`, `Phone (varchar 20, nullable)`, `UserRole (int)`, `CreatedAt`, `UpdatedAt`

Added 2026-10-05 for ASP.NET Core Identity (not in the source report): `NormalizedEmail (varchar 120, unique)`, the upper-cased email Identity looks users up by, so sign-in and the duplicate-email check ignore case; `SecurityStamp (varchar 64)`, changed whenever credentials change so other sessions are signed out; `AccessFailedCount (int)` and `LockoutEnd (datetimeoffset, nullable)`, which back the lockout after repeated wrong passwords. Identity works on this table through `AppleStore.Infrastructure.Identity.UserStore`, so none of Identity's own `AspNet*` tables exist. Roles come from `UserRole` and are added to the sign-in cookie as claims.

### CategoryAttributes
Junction, composite PK `(CategoryId, AttributeId)`, both FK.

### OptionValues
`Id (PK)`, `OptionTypeId (FK -> OptionTypes)`, `Value (nvarchar 60)`

### Products
`Id (PK)`, `CategoryId (FK -> Categories)`, `Name (nvarchar 200)`, `Slug (nvarchar 220)`, `Description (nvarchar max, nullable)`, `BasePrice (decimal(12,2), > 0)`, `Status (bit)`, `CreatedAt`, `UpdatedAt`

**Deviation (2026-09-25, `feat/variant-sync`):** `BasePrice` is nullable, null meaning no price yet ("Contact for price", the user's decision), instead of a fake 0. Added `SortOrder (int)`, the display order within a category, so models list in the reference store's order (newest first).

### UserTokens
`Id (PK)`, `UserId (FK -> Users)`, `Type (int)`, `Token (varchar 100)`, `ExpiredAt`, `UsedAt (nullable)`, `CreatedAt`

**Registration OTP does not use UserTokens.** `UserId` is a required FK, but the
report's own registration use case creates the `User` row only after OTP
confirmation succeeds, so there is nothing for a `UserTokens` row to attach to at
the moment the OTP is generated (see the earlier PLAN discussion, "option A").
`RegistrationService` instead holds the pending email, phone, full name, hashed
password, OTP code, and expiry in `IMemoryCache`, keyed by a generated attempt
id, and only writes to `Users` on confirmation. `UserTokenType.RegisterOtp`
exists in the enum (matching the schema's implied domain) but nothing in the
codebase constructs one yet. It stays reserved for a future design that does
persist a pending registration (which would need `UserTokens.UserId` to become
nullable, a real schema change, not made here). `UserTokenType.ResetPasswordOtp`
does not have this problem: forgot-password always has an existing `User` row to
attach the token to, so it uses `UserTokens` as the schema intends. Built
2026-10-05 (`PasswordResetService`): `Token` holds the code hashed with
Identity's password hasher (never the plain code), `ExpiredAt` is 5 minutes
after creation, `UsedAt` is set when the code is used or replaced by a newer
one, and the code is claimed with a conditional update so it works only once.

### Addresses
`Id (PK)`, `UserId (FK -> Users)`, `Label (nvarchar 60, nullable)`, `FullName (nvarchar 120)`, `Phone (varchar 20)`, `AddressLine (nvarchar 255)`, `Ward/District/City (nvarchar 100, nullable)`, `IsDefault (bit)`

Rules (`ProfileService`, 2026-10-05): a user's first address becomes the
default; making another one default clears the previous one, so there is at
most one; unticking the current default keeps it (another address has to be
made default instead). A user only ever sees and changes their own addresses.

### Carts
`Id (PK)`, `UserId (FK -> Users)`

**Added 2026-10-07 (`feat/cart`):** unique index on `UserId`, one cart per user.

### CompareLists
`Id (PK)`, `UserId (FK -> Users)`

### ProductAttributeValues
Composite PK `(ProductId, AttributeId)`, `ValueText (nvarchar 255, nullable)`, `ValueNumber (decimal, nullable)`

### ProductVariants
`Id (PK)`, `ProductId (FK -> Products)`, `SKU (nvarchar 80)`, `Price (decimal(12,2), > 0)`, `StockQty (int, >= 0)`, `Status (bit)`, `CreatedAt`, `UpdatedAt`

**Deviation (2026-09-25, `feat/variant-sync`):** `Price` is nullable for the same reason as `Products.BasePrice`. When set it is still > 0. Checkout (M3) must refuse a variant with no price.

### Favorites
Composite PK `(UserId, ProductId)`.

### CompareItems
Composite PK `(ListId, ProductId)`, `ListId (FK -> CompareLists)`.

### Orders
`Id (PK)`, `UserId (FK -> Users)`, `OrderStatus (int, see gap below)`, `PaymentStatus (int)`, `Subtotal/DiscountAmount/ShippingFee/TotalAmount (decimal(12,2), >= 0)`, `VoucherCode (varchar 40, nullable)`, `ReceiverName/Phone/AddressLine/Ward/District/City`, `Note (nvarchar 400, nullable)`, `CreatedAt`, `UpdatedAt`

### Reviews
`Id (PK)`, `ProductId (FK -> Products)`, `UserId (FK -> Users)`, `Rating (int, 1-5)`, `Content (nvarchar max, nullable)`, `HasMedia/PurchaseVerified/Status (bit)`, `CreatedAt`

**Added 2026-10-08 to Reviews (`feat/reviews`):** `Reply (nvarchar max, nullable)`, `RepliedAt`, `UpdatedAt` (nullable), and a unique index on `(ProductId, UserId)`. The report lets staff reply to reviews but had no column for it.

### VariantOptions
Composite PK `(VariantId, OptionTypeId)`, `OptionValueId (FK -> OptionValues, not part of the key)`.

Option type codes in use (seeded 2026-09-25): `config` (the configuration card a variant belongs to, e.g. "iPhone 18 Pro Max 256GB ( VN )"), `color`, `region`. A variant with no `config` option belongs to one configuration named after its product.

### ProductImages
`Id (PK)`, `ProductId (FK -> Products)`, `VariantId (FK -> ProductVariants, nullable)`, `ImageUrl (nvarchar 255)`, `SortOrder (int, >= 0)`

### Vouchers (added 2026-10-07, not in the report)
`Id (PK)`, `Code (varchar 40, unique, stored upper case)`, `DiscountType (int: Percent=0, Fixed=1)`, `DiscountValue (decimal(12,2))`, `MinOrderAmount (decimal(12,2), nullable)`, `StartsAt`, `EndsAt`, `UsageLimit (int, nullable)`, `UsedCount (int)`, `IsActive (bit)`, `CreatedAt`, `UpdatedAt`

The report keeps only `Orders.VoucherCode`; BM_VOUCHER_01 describes percent or fixed discounts, all or some products, a minimum order, a usage count and a time window, so the owner chose a table. `Orders.VoucherCode` still holds the code as text, so an order keeps its code even if the voucher row changes later.

### VoucherProducts (added 2026-10-07)
Composite PK `(VoucherId, ProductId)`, both cascading. A voucher with rows here applies only to those products.

### CartItems
`Id (PK)`, `CartId (FK -> Carts)`, `ProductId (FK -> Products)`, `VariantId (FK -> ProductVariants)`, `Quantity (int, >= 1)`

**Added 2026-10-07 (`feat/cart`):** unique index on `(CartId, VariantId)`, one line per variant; adding the variant again raises `Quantity`. `Quantity >= 1` and `<= StockQty` are enforced by `CartService`, not by a check constraint (the schema has none elsewhere either). Note: `ProductVariants.SKU` is not unique (two retired AirTag variants share a SKU with active ones), so the cart form posts the variant id.

### OrderItems
`Id (PK)`, `OrderId (FK -> Orders)`, `ProductId (FK -> Products)`, `VariantId (FK -> ProductVariants)`, `Price (decimal(12,2), >= 0)`, `Quantity (int, > 0)`

### Payments
`Id (PK)`, `OrderId (FK -> Orders)`, `Method (int)`, `Status (int)`, `TxnId (varchar 120, nullable)`, `PaidAmount (decimal(12,2), >= 0)`, `PaidAt (nullable)`, `ProviderRaw (nvarchar max, nullable)`, `CreatedAt`

### Shipments
`Id (PK)`, `OrderId (FK -> Orders)`, `Carrier (nvarchar 60)`, `TrackingNo (varchar 80, nullable)`, `Status (int)`, `Fee (decimal(12,2), >= 0)`, `CreatedAt`, `UpdatedAt`

### ReviewMedia
`Id (PK)`, `ReviewId (FK -> Reviews)`, `MediaUrl (varchar 255)`, `MediaType (int)`, `SortOrder (int, >= 0)`

## Enum ordinals: report-explicit vs implementation choice

The report numbers some status columns explicitly and leaves others (`Số`, plain
numeric) with no stated domain. Where we had to assign ordinals ourselves, the enum
carries a code comment saying so. Summary:

| Enum | Values | Source |
|---|---|---|
| `OrderPaymentStatus` | Unpaid=0, Paid=1 | explicit in report |
| `PaymentStatus` | Pending=0, Success=1, Failed=2 | explicit in report |
| `ShipmentStatus` | AwaitingPickup=0, InTransit=1, Delivered=2, Cancelled=3 | explicit in report |
| `ReviewMediaType` | Image=0, Video=1 | explicit in report |
| `AttributeDataType` | Text=0, Number=1 | inferred from the ValueText/ValueNumber split, not explicit |
| `UserRole` | Customer=0, Employee=1, Admin=2 | our assignment, report gives no domain |
| `UserTokenType` | RegisterOtp=0, ResetPasswordOtp=1 | inferred from the use-case narrative, not explicit |
| `PaymentMethod` | Cod=0, VnPay=1, MoMo=2 | our assignment, report names the three methods but never numbers them |
| `OrderStatus` | Pending=0, Confirmed=1, Shipping=2, Completed=3, Cancelled=4 | report gives the 0-4 domain; 5th value decided 2026-10-05, see below |

## OrderStatus (resolved 2026-10-05)

The schema chapter states the value domain is "0 to 4" (five values) but only
names four: `0 = Cho xu ly (pending)`, `1 = Dang giao (shipping)`,
`2 = Hoan tat (completed)`, `3 = Huy (cancelled)`. Chapter 2's order-tracking
use case separately lists a distinct "confirmed" step between pending and
shipping.

Decision (project owner, 2026-10-05): the fifth value is "confirmed", placed in
lifecycle order, and the later values shift by one:

| Value | Name | Vietnamese |
|---|---|---|
| 0 | Pending | Cho xu ly |
| 1 | Confirmed | Da xac nhan |
| 2 | Shipping | Dang giao |
| 3 | Completed | Hoan tat |
| 4 | Cancelled | Huy |

Shifting was safe because no order rows existed yet and the column has no
default or check constraint, so no migration was needed. The "returned" outcome
from chapter 2 is not modelled.
