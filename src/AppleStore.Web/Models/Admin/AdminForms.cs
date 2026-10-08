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
    public decimal? Price { get; set; }
    public int StockQty { get; set; }
    public bool OnSale { get; set; } = true;
}

public sealed class VariantChangeForm
{
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

    public VoucherDiscountType Type { get; set; }

    [Display(Name = "Discount (percent or VND)")]
    public decimal Value { get; set; }

    [Display(Name = "Minimum order (VND)")]
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
    public VoucherInput ToInput() => new(Code, Type, Value, MinOrderAmount,
        DateTime.SpecifyKind(StartsAt, DateTimeKind.Local).ToUniversalTime(),
        DateTime.SpecifyKind(EndsAt, DateTimeKind.Local).ToUniversalTime(),
        UsageLimit, IsActive, ProductIds);

    public static VoucherForm From(VoucherAdminRow v) => new()
    {
        Code = v.Code,
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

public sealed record VoucherPage(int? Id, VoucherForm Form, int UsedCount, IReadOnlyList<(int Id, string Name)> Products, string? Error);

public sealed record UsersPage(IReadOnlyList<UserAdminRow> Users, string? Search, int CurrentUserId);

public sealed record ReportPage(DateOnly From, DateOnly To, SalesReport? Report, string? Error);
