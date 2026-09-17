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

### CategoryAttributes
Junction, composite PK `(CategoryId, AttributeId)`, both FK.

### OptionValues
`Id (PK)`, `OptionTypeId (FK -> OptionTypes)`, `Value (nvarchar 60)`

### Products
`Id (PK)`, `CategoryId (FK -> Categories)`, `Name (nvarchar 200)`, `Slug (nvarchar 220)`, `Description (nvarchar max, nullable)`, `BasePrice (decimal(12,2), > 0)`, `Status (bit)`, `CreatedAt`, `UpdatedAt`

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
attach the token to, so it can use `UserTokens` as the schema intends once that
use case is built (M1, not yet started).

### Addresses
`Id (PK)`, `UserId (FK -> Users)`, `Label (nvarchar 60, nullable)`, `FullName (nvarchar 120)`, `Phone (varchar 20)`, `AddressLine (nvarchar 255)`, `Ward/District/City (nvarchar 100, nullable)`, `IsDefault (bit)`

### Carts
`Id (PK)`, `UserId (FK -> Users)`

### CompareLists
`Id (PK)`, `UserId (FK -> Users)`

### ProductAttributeValues
Composite PK `(ProductId, AttributeId)`, `ValueText (nvarchar 255, nullable)`, `ValueNumber (decimal, nullable)`

### ProductVariants
`Id (PK)`, `ProductId (FK -> Products)`, `SKU (nvarchar 80)`, `Price (decimal(12,2), > 0)`, `StockQty (int, >= 0)`, `Status (bit)`, `CreatedAt`, `UpdatedAt`

### Favorites
Composite PK `(UserId, ProductId)`.

### CompareItems
Composite PK `(ListId, ProductId)`, `ListId (FK -> CompareLists)`.

### Orders
`Id (PK)`, `UserId (FK -> Users)`, `OrderStatus (int, see gap below)`, `PaymentStatus (int)`, `Subtotal/DiscountAmount/ShippingFee/TotalAmount (decimal(12,2), >= 0)`, `VoucherCode (varchar 40, nullable)`, `ReceiverName/Phone/AddressLine/Ward/District/City`, `Note (nvarchar 400, nullable)`, `CreatedAt`, `UpdatedAt`

### Reviews
`Id (PK)`, `ProductId (FK -> Products)`, `UserId (FK -> Users)`, `Rating (int, 1-5)`, `Content (nvarchar max, nullable)`, `HasMedia/PurchaseVerified/Status (bit)`, `CreatedAt`

### VariantOptions
Composite PK `(VariantId, OptionTypeId)`, `OptionValueId (FK -> OptionValues, not part of the key)`.

### ProductImages
`Id (PK)`, `ProductId (FK -> Products)`, `VariantId (FK -> ProductVariants, nullable)`, `ImageUrl (nvarchar 255)`, `SortOrder (int, >= 0)`

### CartItems
`Id (PK)`, `CartId (FK -> Carts)`, `ProductId (FK -> Products)`, `VariantId (FK -> ProductVariants)`, `Quantity (int, >= 1)`

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
| `OrderStatus` | see below | explicit but incomplete |

## Open gap: OrderStatus

This is the most important unresolved item from the source report. The schema
chapter states the value domain is "0 to 4" (five values) but only names four:
`0 = Cho xu ly (pending)`, `1 = Dang giao (shipping)`, `2 = Hoan tat (completed)`,
`3 = Huy (cancelled)`. Chapter 2's order-tracking use case separately lists six
narrative states, including a distinct "confirmed" step between pending and
shipping, and a "returned" outcome distinct from cancelled, that do not map onto
this four-value numbering.

`AppleStore.Domain.Enums.OrderStatus` currently implements only the four explicit
values, unchanged from the report, with a code comment flagging this gap. Do not
add a fifth value without the team deciding what it represents (most likely
"confirmed" or "returned") and whether the existing three non-zero values need to
shift. This decision should happen before the order-management milestone in
`docs/roadmap.md` starts, not silently during it.
