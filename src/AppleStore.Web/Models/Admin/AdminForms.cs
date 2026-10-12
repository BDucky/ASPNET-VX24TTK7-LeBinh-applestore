using System.ComponentModel.DataAnnotations;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;

namespace AppleStore.Web.Models.Admin;

public sealed class ProductForm
{
    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(4000)]
    public string? Description { get; set; }

    [Display(Name = "Category")]
    public int CategoryId { get; set; }

    [Display(Name = "Base price (VND)")]
    [WholeDong]
    public decimal? BasePrice { get; set; }

    [Display(Name = "On sale")]
    public bool OnSale { get; set; } = true;

    [Display(Name = "Photo")]
    public string? ImageUrl { get; set; }

    public long Version { get; set; }

    public ProductInput ToInput() => new(Name, Description, CategoryId, BasePrice, OnSale, ImageUrl);
}

public sealed class VariantAddForm
{
    public string? Sku { get; set; }
    public string? Configuration { get; set; }
    public string? Color { get; set; }
    public string? Region { get; set; }
    [WholeDong]
    public decimal? Price { get; set; }
    public int StockQty { get; set; }
    public bool OnSale { get; set; } = true;
}

public sealed class VariantChangeForm
{
    [WholeDong]
    public decimal? Price { get; set; }
    public int StockQty { get; set; }
    public bool OnSale { get; set; }
    public int SeenStock { get; set; }
    public long Version { get; set; }
}

public sealed class VoucherForm
{
    [StringLength(40)]
    public string? Code { get; set; }

    // A promotion's name; vouchers have none.
    [StringLength(120)]
    public string? Name { get; set; }

    public VoucherDiscountType Type { get; set; }

    [Display(Name = "Discount (percent or VND)")]
    [WholeDong(nameof(Type), VoucherDiscountType.Fixed)]
    public decimal Value { get; set; }

    [Display(Name = "Minimum order (VND)")]
    [WholeDong]
    public decimal? MinOrderAmount { get; set; }

    [Display(Name = "Starts (local time)")]
    public DateTime StartsAt { get; set; }

    [Display(Name = "Ends (local time)")]
    public DateTime EndsAt { get; set; }

    [Display(Name = "Usage limit")]
    public int? UsageLimit { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public List<int> ProductIds { get; set; } = [];

    public long Version { get; set; }

    // Admins type the shop's local time; vouchers are checked in UTC.
    // The kind is the page's, never the post's. Which fields a kind keeps is
    // AdminVoucherService's rule, so everything posted is passed on.
    public VoucherInput ToInput(VoucherKind kind) =>
        new(Code, Type, Value, MinOrderAmount, Utc(StartsAt), Utc(EndsAt), UsageLimit, IsActive, ProductIds, kind, Name);

    private static DateTime Utc(DateTime local) => DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    public static VoucherForm From(VoucherAdminRow v) => new()
    {
        Code = v.Code,
        Name = v.Name,
        Type = v.Type,
        Value = v.Value,
        MinOrderAmount = v.MinOrderAmount,
        StartsAt = DateTime.SpecifyKind(v.StartsAt, DateTimeKind.Utc).ToLocalTime(),
        EndsAt = DateTime.SpecifyKind(v.EndsAt, DateTimeKind.Utc).ToLocalTime(),
        UsageLimit = v.UsageLimit,
        IsActive = v.IsActive,
        ProductIds = [.. v.ProductIds],
        Version = v.Version,
    };
}

public sealed record ProductPage(int? Id, ProductForm Form, string? Slug, IReadOnlyList<VariantEdit> Variants,
    IReadOnlyList<(int Id, string Name)> Categories, IReadOnlyList<string> Images, string? Error);

public sealed record VoucherPage(int? Id, VoucherForm Form, int UsedCount, IReadOnlyList<(int Id, string Name)> Products, string? Error,
    VoucherKind Kind = VoucherKind.Code)
{
    public DiscountPageText Text => DiscountPageText.For(Kind);
}

public sealed record UsersPage(IReadOnlyList<UserAdminRow> Users, string? Search, int CurrentUserId);

public sealed record ReportPage(DateOnly From, DateOnly To, SalesReport? Report, string? Error);

// BM_PRICE_01. Value is nullable so a box left empty or unreadable is told
// apart from a real number (model state reports it).
public sealed class PriceBatchForm
{
    public List<int> ProductIds { get; set; } = [];
    public int? CategoryId { get; set; }
    public PriceBatchMode Mode { get; set; }
    [WholeDong(nameof(Mode), PriceBatchMode.Amount)]
    public decimal? Value { get; set; }

    public PriceBatchInput ToInput() => new(ProductIds, CategoryId, Mode, Value ?? 0m);
}

public sealed record PricesPage(PriceBatchForm Form, IReadOnlyList<(int Id, string Name)> Products, IReadOnlyList<(int Id, string Name)> Categories,
    IReadOnlyList<PriceChangeRow> History, int? ProductFilter, string? Error);

// An amount of money typed in a staff form is whole dong without separators.
// "1.000" binds as 1 with three decimals (scale 3), so it is refused instead
// of saving one dong where a thousand was meant (found 2026-10-09). One
// attribute for every money field; a gate property and value (an enum field
// on the same form) narrow it to the mode or type where the field is
// actually money, since the same box can also hold a percent.
[AttributeUsage(AttributeTargets.Property)]
public sealed class WholeDongAttribute : ValidationAttribute
{
    private readonly string? _gateProperty;
    private readonly object? _gateValue;

    public WholeDongAttribute()
    {
    }

    public WholeDongAttribute(string gateProperty, object gateValue)
    {
        _gateProperty = gateProperty;
        _gateValue = gateValue;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (_gateProperty is not null)
        {
            var gate = validationContext.ObjectType.GetProperty(_gateProperty)?.GetValue(validationContext.ObjectInstance);
            if (!Equals(gate, _gateValue))
                return ValidationResult.Success;
        }
        return value as decimal? is not { } v || v.Scale == 0
            ? ValidationResult.Success
            : new ValidationResult("Type numbers only, without dots or commas (for example 24990000).");
    }
}

// Use case 20. Rows with neither a variant nor a quantity are empty rows of
// the form and are skipped.
public sealed class StockReceiptForm
{
    [StringLength(200)]
    public string? Supplier { get; set; }

    [StringLength(400)]
    public string? Note { get; set; }

    public Guid FormKey { get; set; }

    public List<StockLineForm> Lines { get; set; } = [];

    public IEnumerable<StockLineForm> FilledLines => Lines.Where(l => l.VariantId is not null || l.Quantity is not null);

    public StockReceiptInput ToInput() => new(Supplier, Note, FormKey,
        FilledLines.Select(l => new StockReceiptLineInput(l.VariantId ?? 0, l.Quantity ?? 0, l.UnitCost ?? 0m)).ToList());
}

public sealed class StockLineForm
{
    public int? VariantId { get; set; }
    public int? Quantity { get; set; }
    [WholeDong]
    public decimal? UnitCost { get; set; }
}

public sealed record StockNewPage(StockReceiptForm Form, IReadOnlyList<StockLevelRow> Variants, string? Error);

// Use case 21. Intent "quote" prices the sale; "sell" completes it at
// ExpectedTotal, the total the staff member was shown.
public sealed class SaleForm
{
    public Guid FormKey { get; set; }
    public List<SaleLineForm> Lines { get; set; } = [];

    [StringLength(40)]
    public string? VoucherCode { get; set; }

    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;

    [StringLength(100)]
    public string? CustomerName { get; set; }

    [StringLength(20)]
    public string? CustomerPhone { get; set; }

    [StringLength(256)]
    public string? CustomerEmail { get; set; }

    public decimal? ExpectedTotal { get; set; }
    public string? Intent { get; set; }

    // What was priced (lines and voucher), sent back with "Complete sale": a
    // sale changed after pricing is priced again, never sold unseen
    // (review 2026-10-10: a product switched for one at the same price).
    public string? QuotedFor { get; set; }

    public string Signature() =>
        string.Join(",", FilledLines.Select(l => $"{l.VariantId}x{l.Quantity}")) + "|" + (VoucherCode?.Trim().ToUpperInvariant() ?? "");

    public IReadOnlyList<SaleLineInput> FilledLines =>
        Lines.Where(l => l.VariantId is not null || l.Quantity is not null).Select(l => new SaleLineInput(l.VariantId ?? 0, l.Quantity ?? 0)).ToList();

    public SaleInput ToInput() => new(FormKey, FilledLines, VoucherCode, Method, CustomerName, CustomerPhone, CustomerEmail);
}

public sealed class SaleLineForm
{
    public int? VariantId { get; set; }
    public int? Quantity { get; set; }
}

public sealed record SalePage(SaleForm Form, IReadOnlyList<StockLevelRow> Variants, SaleQuote? Quote, string? Error);
