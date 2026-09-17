# UX research

Concrete UI/UX patterns from real selling sites, gathered to inform M2 (catalog),
M3 (cart/checkout), M5 (order tracking), and M8 (reviews) in `docs/roadmap.md`.
This is reference material to draw on while building those milestones, not a
spec: nothing here overrides the report's own requirements in
`docs/requirements.md`, and nothing here is committed to as a UI decision until
the relevant milestone actually builds it.

**How this was gathered and how much to trust each part:** apple.com and the
three Vietnamese sites were fetched live on 2026-09-17. Amazon and Shopee both
blocked or could not serve a live fetch (Amazon returned 503 as bot traffic,
Shopee is a client-rendered SPA with no server HTML), so that section is general
UX knowledge, not a live citation, marked as such below. A few specific pages
(apple.com's live price-update behavior on the configurator, the VN sites'
order-tracking pages) could not be reached either; noted where relevant rather
than guessed at.

## Apple.com (live-fetched)

**Product listing** (`apple.com/iphone`): horizontal cards under "Explore the
lineup," not a dense grid. Each card: name, multi-angle image, color swatches
shown inline (not behind a click), one-line tagline, two CTAs ("Learn more" plus
"Buy"/"View pricing"/"Pre-order"). No price on the tile itself, no filter/sort
UI, no ratings anywhere on the site.

**Configurator**: color is chosen before storage in the marketing flow, both
render as swatches/cards, not dropdowns. Live price-per-selection JavaScript
behavior could not be observed (client-rendered); only confirmed the choice
controls exist.

**Comparison** (`apple.com/iphone/compare/`): a dedicated page, dropdown model
pickers, renders as a spec table with each model as a column, rows grouped by
display, design, performance, camera, battery, durability, connectivity,
storage. This maps directly onto this project's `CompareList` / `CompareItem` +
`Attributes` / `ProductAttributeValue` tables: a spec table keyed by
`Attributes.Name` per row and compared products as columns needs no schema
change, just a Razor view.

**Structural takeaways, not visual ones** (Apple's budget and animation team
are not ours to copy):
- A product tile partial view (image, name, color swatches, tagline, two CTAs)
  is cheap and reusable. Unlike Apple, keep a starting price on the tile: the
  report's own functional requirements table calls for it explicitly.
- Apple shows no reviews anywhere. Not a precedent to follow here: the report
  specifies `Reviews` / `ReviewMedia` as a real use case (M8), build it
  regardless of what Apple itself does.
- The comparison table is directly implementable server-side, no JS framework:
  loop `CompareItem -> Product -> ProductAttributeValue` per attribute row.
- The variant picker (color as swatches, storage as a second control group)
  matches `OptionType` / `OptionValue` / `VariantOption` as already modeled, no
  changes needed there.

## Vietnamese Apple resellers: CellphoneS, Thế Giới Di Động, FPT Shop (live-fetched)

**Price display**, consistent across all three: sale price bold and large,
original price struck through and secondary, the discount shown two ways at
once, a red percentage badge (for example "-9%") and an absolute savings line
("Giảm 3.400.000đ"). All three sites lead with what the customer actually pays
and treat the discount as supporting detail, not the reverse. This matters
because a literal read of the report's formula (`Tổng tiền = Σ(qty x price) -
giảm giá + phí ship`) could just as easily produce a UI that leads with list
price and shows the discount as an afterthought; the local convention is the
opposite, and `Order.DiscountAmount` already stores what's needed to render
both the percentage and the absolute figure.

**Stock and social proof**: FPT Shop uses "Hàng sắp về" / "Đặt trước ngay" with
countdown timers for pre-order items. CellphoneS uses "Sẵn hàng" / "Hàng mới về"
/ "Hàng đặt trước". TGDD adds a sold-count badge ("Đã bán 4,3k") and star
ratings directly on the listing card, not just the product page. Our schema has
`ProductVariant.StockQty` and `Status` but no sold-count field; that's a real
gap if TGDD-style social proof is wanted later, not something to add
speculatively now.

**Compare vs favorite, as two distinct features**: CellphoneS has an explicit
"Thêm vào so sánh" checkbox per card, matching `CompareList` / `CompareItem`
directly. TGDD uses a heart/favorite icon instead, matching `Favorite`, a
different feature. Both exist in our schema already; the UI should offer both,
not conflate them into one icon.

**Variant picker, an open decision for M2**: on CellphoneS, storage tiers are
separate links to different product pages, not an in-page selector; color is an
in-page swatch picker with a per-swatch price shown. Our `ProductVariant` /
`VariantOption` model supports either approach (variants can render as
different pages or as in-page controls). M2 should decide this explicitly
rather than default to one without discussion.

**Payment methods near the buy button**: COD is not the headline choice on
either site, it is outnumbered by digital options. VNPay and MoMo (both already
in our `PaymentMethod` enum) sit alongside Apple Pay, ZaloPay, Kredivo, Fundiin,
and 0% installment plans. Installment and trade-in are out of scope for this
project per the report; noting them only because they visually dominate the
real buy box, so our simpler COD/VNPay/MoMo choice will look sparse by
comparison in M4. That is expected, not a defect.

**Checkout address shape**: could not reach a live checkout form on any of the
three sites (all require items in cart/session first). The standard Vietnamese
e-commerce convention, well-established and not site-specific, is a cascading
Tỉnh/Thành phố -> Quận/Huyện -> Phường/Xã picker. This matches `Address` and
`Order`'s `City` / `District` / `Ward` fields exactly; no schema concern for M3.

**Order tracking**: not reachable anonymously on any of the three sites in this
pass, either behind login or the guessed URLs were wrong. Flagged as
unresearched rather than guessed at. If this matters when M5 starts, a follow-up
research pass should search for the correct public tracking URLs rather than
assume a pattern from the sections above generalizes to it.

## Amazon and Shopee (general knowledge, not live-verified)

Both live fetches failed (Amazon 503'd as bot traffic across product, search,
and homepage attempts; Shopee served no usable server HTML on either the .vn or
.com domain, being a client-rendered SPA). What follows is general UX knowledge
about these platforms, not a citation from a page actually seen today.

**Search and filter**: left-sidebar facets (price range, brand/category
checkboxes, star-rating filter), a top sort dropdown (relevance, price, newest,
rating), a visible result count. Product cards show thumbnail, price with
strikethrough-and-badge if discounted, star rating plus review count, and on
Shopee a sold-count, all clustered under the price as social proof.

**Cart**: line items with thumbnail, variant text ("128GB, Blue"), a quantity
stepper, remove link, and a subtotal that updates without a full page reload.
Empty-cart state is a centered icon, a short message, and a CTA back to
browsing.

**Reviews**: a summary block (average score, total count) plus a five-row
horizontal bar chart of the rating distribution, each bar filtering reviews by
that star value when clicked. Individual reviews show reviewer name, star
rating, date, a "Verified Purchase" badge, review text, and a photo/video strip
if attached. This maps directly onto existing fields: the distribution chart is
`Reviews` grouped by `Rating`, the badge is `Review.PurchaseVerified`, the
photo strip is `ReviewMedia`. All server-renderable with no client framework: a
`<div>` per star row with a CSS width percentage computed from the grouped
count.

**Wishlist and comparison**: a heart icon on cards and the product page toggles
favorite state, matching `Favorite`. Comparison is weaker on both platforms
than on Apple.com or CellphoneS, mostly an implicit "similar items" spec table
on Amazon rather than a dedicated add-to-compare flow; our `CompareList` /
`CompareItem` model is closer to the electronics-retailer pattern (Apple,
CellphoneS) than to Amazon/Shopee's.

## What this suggests for the roadmap, concretely

| Milestone | Open decision or concrete pattern to use |
|---|---|
| M2 (catalog) | Decide variant-picker approach explicitly: separate pages per storage tier (CellphoneS) vs in-page selector (Apple). Product tile: image, name, color swatches, starting price, tagline. Comparison page: spec table, attributes as rows, products as columns. |
| M2 (catalog) | Price display leads with the sale price; shows both percentage-off and absolute savings from `DiscountAmount`, not list price first. |
| M3 (cart/checkout) | Address form uses the 3-level Tỉnh/Quận/Phường cascade, already matches `Address`/`Order` fields. Cart: quantity stepper, remove, live subtotal (can start as a full-page-reload form, add AJAX later, not a blocker). |
| M4 (payment) | Expect COD/VNPay/MoMo to look sparse next to real VN sites' 6+ payment options. That is in scope as specified by the report; do not silently add installment/trade-in to match competitors, that would be scope the report never asked for. |
| M5 (order tracking) | VN sites' tracking pages were unreachable in this pass; needs its own research or the report's own use-case spec, not an assumption carried over from this doc. |
| M8 (reviews) | Rating-distribution bar chart computed server-side from `Reviews` grouped by `Rating`. Verified-purchase badge and review-photo strip are direct renders of `Review.PurchaseVerified` and `ReviewMedia`, cheapest to implement first. |
| Schema (not urgent) | No live site's convention requires a schema change. A sold-count field (TGDD-style social proof) is the one gap noted; only add it if a milestone actually decides to use it, not preemptively. |
