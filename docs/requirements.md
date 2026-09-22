# Requirements

Source: "Do an mon hoc Cong nghe phan mem, Xay dung website Apple Store", HCMUTE,
Khoa Cong nghe thong tin, 2025. This document transcribes the requirements
chapters of that report so they live alongside the code instead of only in a PDF.
The source report's own byline names a group ("Nhom 05"); this repository is an
individual submission built from that report's requirements, not a group project.

## Current-state problems the project addresses

- Product information is fragmented across marketplaces and resellers; the
  difference between variants (color, storage) is often unclear.
- Cart and order management is inconvenient, with frequent errors when changing
  quantity or paying cash on delivery.
- Stock handling and reporting is manual (spreadsheets), which causes mistakes.
- Online payment integration is unstable; a failed callback from VNPay/MoMo can
  lose an order.

## Actors

| Actor | Vietnamese | Responsibilities |
|---|---|---|
| Guest | Khach | Register (OTP), log in, forgot password (OTP), search/filter/view products and variants, compare products |
| Customer | Nguoi dung | Everything Guest can do, plus: update profile, manage delivery addresses, cart (add/remove/edit), checkout (COD or VNPay/MoMo), track order status, receive tracking and confirmation emails, write reviews after a verified purchase |
| Employee | Nhan vien | Stock check, goods intake, sell in person, create intake/sales receipts, process orders (confirm, pack, assign tracking, update delivery status), update prices and run promotions, reply to or remove reviews, produce sales reports |
| Admin | Quan tri vien | CRUD categories/products/variants/images/attributes, manage vouchers and promotion policies, manage users and staff, view aggregate reports, system configuration |

## Use cases (37)

Grouped by the actor who initiates them.

**Guest / Customer, account and auth**
1. Dang ky tai khoan (register account, OTP)
2. Gui OTP qua email (send OTP by email)
3. Dang nhap (log in)
4. Quen mat khau (forgot password, OTP)
5. Doi mat khau (change password)
6. Cap nhat thong tin ca nhan (update profile)

**Guest / Customer, catalog**
7. Tim kiem san pham (search products)
8. Loc san pham (filter products)
9. Xem chi tiet san pham (view product detail)
10. So sanh san pham (compare products)

**Customer, cart and checkout**
11. Them san pham vao gio (add to cart)
12. Xoa san pham khoi gio (remove from cart)
13. Cap nhat so luong trong gio (update cart quantity)
14. Xem gio hang (view cart)
15. Dat hang (place order)
16. Ap dung voucher (apply voucher)
17. Thanh toan (pay: COD / VNPay / MoMo)

**Customer, post-purchase**
18. Tra cuu don hang (look up order)
19. Theo doi van chuyen (track shipment)

**Employee, stock and sales**
20. Nhap hang vao kho (stock intake)
21. Ban hang (in-person sale)
22. Xac nhan don hang (confirm order)
23. Gan van don, tracking (assign tracking number)
24. Cap nhat trang thai don (update order status)

**Admin, catalog and promotions**
25. Them san pham moi (add product)
26. Sua san pham (edit product)
27. Xoa san pham (delete product)
28. Tao chuong trinh khuyen mai (create promotion)
29. Them voucher (add voucher)
30. Sua voucher (edit voucher)
31. Xoa voucher (delete voucher)

**Reviews and reporting**
32. Gui danh gia san pham (submit product review)
33. Bao cao tinh hinh kinh doanh (business performance report)
34. Bao cao doanh thu (revenue report)
35. Xuat bao cao (export report, Excel/PDF)
36. In hoa don (print invoice)
37. Gui email xac nhan don (send order confirmation email)

## Functional requirements (business logic)

| # | Function | Formula / rule | Related form |
|---|---|---|---|
| 1 | Product management | A product belongs to one or more categories; attributes (color, size, etc.) are managed per category | BM_CATEGORY_ADD_01, BM_CATEGORY_EDIT_01 |
| 2 | Search and filter | By name, keyword, SKU; filter by price, category, brand; sort by price or newest | |
| 3 | Cart and checkout | `total = sum(qty * price) - discount + shipping fee`; cart stored per session or DB | BM_CART_CHECKOUT_01 |
| 4 | Order management | Customer views order history; Admin/Employee updates order status | BM_ORDER_HISTORY_01 |
| 5 | Voucher management | Percent or fixed discount; applies to all products, some products, or orders over a minimum; limited by usage count and time window | BM_VOUCHER_01 |
| 6 | Stock intake | Goods from a supplier, duplicate SKU check, `closing stock = opening stock + intake` | BM_STOCK_01 |
| 7 | Sales / invoice creation | `total = sum(qty * unit_price) - discount + shipping fee + tax`; supports batch invoice printing | BM_INVOICE_01 |
| 8 | Price and promotion updates | Batch update, price-change history log, new price applies automatically | BM_PRICE_01 |
| 9 | Inbound-outbound report | By period (week/month/quarter) | BM_REPORT_INOUT_01 |
| 10 | Revenue report | By day/week/month/year, `revenue = sum(completed orders) - refunds`, includes top sellers | BM_REPORT_REVENUE_01 |
| 11 | Product search (data access) | By SKU/name/category, returns stock, variant, price | BM_SEARCH_01 |
| 12 | Revenue calculation | `revenue = sum(qty * unit_price) - discount` | BM_CALC_REVENUE_01 |
| 13 | Stock calculation | `closing stock = opening stock + intake - sold`, auto-updates after each transaction | BM_CALC_STOCK_01 |
| 14 | Order tracking | By order code, phone, or email; returns status and tracking; notifies by email/SMS | BM_ORDER_TRACK_01 |

## Non-functional requirements

| Category | Requirement |
|---|---|
| Performance | Create an order in <= 5s; search products in <= 3s; load a report in <= 30s |
| Usability | Easy search, filter, and checkout flows |
| Security | HTTPS; JWT for session; rate-limited OTP and login attempts |
| Extensibility | Module-based design so new payment gateways or features can be added without rework |
| Data interoperability | Import/export CSV/Excel/PDF for categories, products, users, and revenue reports |
| Maintainability | Clear logging, low-stock alerts, a dashboard for system health |

## Order status flow (narrative, see docs/data-model.md for the schema gap)

The requirements chapter describes the flow as: new -> processing -> cancelled or
shipping -> completed or returned. The order-tracking use case separately lists six
states: pending confirmation, confirmed, shipping, delivered, returned, cancelled.
Neither maps one-to-one onto the four values the schema chapter actually names.
This gap is carried into `OrderStatus` in `AppleStore.Domain.Enums` deliberately
unresolved; see `docs/data-model.md`.

## BM_* form glossary

The report defines paper-form templates for each business function, referenced by
code in the functional requirements table above: `BM_CATEGORY_ADD_01`,
`BM_CATEGORY_EDIT_01`, `BM_CART_CHECKOUT_01`, `BM_ORDER_HISTORY_01`,
`BM_VOUCHER_01`, `BM_STOCK_01`, `BM_INVOICE_01`, `BM_PRICE_01`,
`BM_REPORT_INOUT_01`, `BM_REPORT_REVENUE_01`, `BM_SEARCH_01`,
`BM_CALC_REVENUE_01`, `BM_CALC_STOCK_01`, `BM_ORDER_TRACK_01`. These describe what
data each operation captures (for example `BM_CART_CHECKOUT_01` lists user info,
cart lines, payment method, and shipping info) and are useful as a checklist when
building the corresponding screen or endpoint, not as literal UI to replicate.
