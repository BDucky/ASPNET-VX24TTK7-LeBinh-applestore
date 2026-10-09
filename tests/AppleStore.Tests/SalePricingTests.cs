using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// Use case 28: SalePrices on SQLite. A promotion is a Vouchers row of Kind
// Automatic; the owner's rules (2026-10-09): it runs inside its window, the
// largest discount wins when two cover a product, and the amount comes from
// DiscountMath. Claude's rule, stated before building: a discount that would
// take a price to 0 or below is not applied, so nothing is ever free.
public sealed class SalePricingTests : IDisposable
{
    internal static readonly DateTime Now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly ShopTestDb _shop = new();
    private readonly AppDbContext _db;
    private readonly int _phone;
    private readonly int _pods;

    public SalePricingTests()
    {
        _db = _shop.Context();
        var phone = NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 1m);
        var pods = NewProduct(NewCategory("AirPods", "airpods"), "AirPods 4", "airpods-4", 1m);
        _db.AddRange(phone, pods);
        _db.SaveChanges();
        (_phone, _pods) = (phone.Id, pods.Id);
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    // Also used by the catalog, cart, checkout and compare tests.
    internal static int AddPromotion(AppDbContext db, string name, VoucherDiscountType type, decimal value, DateTime? starts = null, DateTime? ends = null,
        bool active = true, VoucherKind kind = VoucherKind.Automatic, params int[] onlyProducts)
    {
        var promotion = new Voucher
        {
            Kind = kind,
            Name = name,
            Code = kind == VoucherKind.Code ? name.ToUpperInvariant() : null,
            DiscountType = type,
            DiscountValue = value,
            IsActive = active,
            StartsAt = starts ?? Now.AddDays(-1),
            EndsAt = ends ?? Now.AddDays(30),
            CreatedAt = Now,
            UpdatedAt = Now,
        };
        db.Vouchers.Add(promotion);
        db.SaveChanges();
        db.VoucherProducts.AddRange(onlyProducts.Select(p => new VoucherProduct { VoucherId = promotion.Id, ProductId = p }));
        db.SaveChanges();
        return promotion.Id;
    }

    private async Task<SalePrice> PriceAsync(int productId, decimal? basePrice, DateTime? at = null) =>
        (await SalePrices.LoadAsync(_db, at ?? Now)).For(productId, basePrice);

    [Fact]
    public async Task A_percent_promotion_lowers_the_price_and_keeps_the_old_one()
    {
        AddPromotion(_db, "Black Friday", VoucherDiscountType.Percent, 10m);

        var price = await PriceAsync(_phone, 24_990_005m);

        // 2.499.000,5 rounds away from zero to 2.499.001.
        Assert.Equal(new SalePrice(22_491_004m, 24_990_005m, "Black Friday"), price);
    }

    [Fact]
    public async Task A_fixed_promotion_takes_its_amount_off()
    {
        AddPromotion(_db, "Back to school", VoucherDiscountType.Fixed, 500_000m);

        Assert.Equal(new SalePrice(6_490_000m, 6_990_000m, "Back to school"), await PriceAsync(_pods, 6_990_000m));
    }

    [Theory]
    [InlineData("not started")]
    [InlineData("ended")]
    [InlineData("switched off")]
    [InlineData("a voucher code")]
    public async Task Only_a_running_automatic_promotion_counts(string which)
    {
        switch (which)
        {
            case "not started": AddPromotion(_db, "Soon", VoucherDiscountType.Percent, 10m, starts: Now.AddSeconds(1)); break;
            case "ended": AddPromotion(_db, "Over", VoucherDiscountType.Percent, 10m, ends: Now.AddSeconds(-1)); break;
            case "switched off": AddPromotion(_db, "Off", VoucherDiscountType.Percent, 10m, active: false); break;
            default: AddPromotion(_db, "Typed", VoucherDiscountType.Percent, 10m, kind: VoucherKind.Code); break;
        }

        Assert.Equal(new SalePrice(1_000_000m), await PriceAsync(_phone, 1_000_000m));
    }

    // Same window rule as a voucher: both ends count.
    [Fact]
    public async Task Both_ends_of_the_window_count()
    {
        AddPromotion(_db, "Day", VoucherDiscountType.Fixed, 1_000m, starts: Now, ends: Now.AddHours(1));

        Assert.Equal(999_000m, (await PriceAsync(_phone, 1_000_000m, Now)).Price);
        Assert.Equal(999_000m, (await PriceAsync(_phone, 1_000_000m, Now.AddHours(1))).Price);
        Assert.Equal(1_000_000m, (await PriceAsync(_phone, 1_000_000m, Now.AddHours(1).AddSeconds(1))).Price);
    }

    [Fact]
    public async Task A_promotion_for_some_products_leaves_the_others_alone()
    {
        AddPromotion(_db, "Pods week", VoucherDiscountType.Percent, 20m, onlyProducts: [_pods]);

        Assert.Equal(new SalePrice(1_000_000m), await PriceAsync(_phone, 1_000_000m));
        Assert.Equal(800_000m, (await PriceAsync(_pods, 1_000_000m)).Price);
    }

    // 10% beats 500.000 on 24.990.000, but 500.000 beats 10% on 3.000.000.
    [Fact]
    public async Task When_two_cover_a_product_the_larger_discount_wins_for_that_price()
    {
        AddPromotion(_db, "Ten percent", VoucherDiscountType.Percent, 10m);
        AddPromotion(_db, "Half a million", VoucherDiscountType.Fixed, 500_000m);

        Assert.Equal(new SalePrice(22_491_000m, 24_990_000m, "Ten percent"), await PriceAsync(_phone, 24_990_000m));
        Assert.Equal(new SalePrice(2_500_000m, 3_000_000m, "Half a million"), await PriceAsync(_pods, 3_000_000m));
    }

    [Fact]
    public async Task A_discount_that_would_make_it_free_is_not_applied()
    {
        AddPromotion(_db, "Too much", VoucherDiscountType.Fixed, 1_000_000m);
        AddPromotion(_db, "Small", VoucherDiscountType.Fixed, 100_000m);
        AddPromotion(_db, "All of it", VoucherDiscountType.Percent, 100m);

        Assert.Equal(new SalePrice(890_000m, 990_000m, "Small"), await PriceAsync(_pods, 990_000m));
        Assert.Equal(new SalePrice(1_000_000m - 100_000m, 1_000_000m, "Small"), await PriceAsync(_phone, 1_000_000m));
    }

    // Only the admin pages refuse these; a row written another way is skipped.
    [Theory]
    [InlineData(VoucherDiscountType.Fixed, -100_000)]
    [InlineData(VoucherDiscountType.Percent, -10)]
    [InlineData(VoucherDiscountType.Fixed, 0)]
    public async Task A_zero_or_negative_promotion_changes_nothing(VoucherDiscountType type, int value)
    {
        AddPromotion(_db, "Broken", type, value);

        Assert.Equal(new SalePrice(1_000_000m), await PriceAsync(_phone, 1_000_000m));
    }

    [Fact]
    public async Task No_price_stays_no_price()
    {
        AddPromotion(_db, "Any", VoucherDiscountType.Percent, 10m);

        Assert.Equal(new SalePrice(null), await PriceAsync(_phone, null));
    }
}
