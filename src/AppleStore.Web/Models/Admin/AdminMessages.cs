using AppleStore.Infrastructure.Services;

namespace AppleStore.Web.Models.Admin;

// The one place admin results become sentences.
public static class AdminMessages
{
    public const string ProductSaved = "Product saved.";
    public const string VariantAdded = "Variant added.";
    public const string VariantSaved = "Variant saved.";
    public const string OffSale = "Taken off sale. Orders that include it are not changed.";
    public const string OnSale = "Back on sale.";
    public const string VoucherSaved = "Voucher saved.";
    public const string VoucherDeleted = "Voucher deleted. Orders that used it keep their code.";
    public const string RoleChanged = "Role changed. Their open sessions were signed out.";
    public const string OwnRole = "You cannot change your own role.";
    public const string InvalidRole = "Pick a role from the list.";
    public const string SaveFailed = "We could not save that. Please try again.";
    // A number the model binder could not read binds to null or 0; it is never saved.
    public const string UnreadableNumber = "Type numbers only, without dots or commas (for example 24990000).";

    public static string For(AdminCatalogResult result) => result.Outcome switch
    {
        AdminCatalogOutcome.MissingName => "Enter a name.",
        AdminCatalogOutcome.NameTaken => "Another product already uses this name (their page addresses would clash).",
        AdminCatalogOutcome.UnknownCategory => "Pick a category.",
        AdminCatalogOutcome.ImageNotInLibrary => "Pick a photo from the list.",
        AdminCatalogOutcome.InvalidPrice => "A price is more than 0, or empty for \"Contact for price\".",
        AdminCatalogOutcome.InvalidStock => "Stock cannot be below 0.",
        AdminCatalogOutcome.MissingSku => "Enter a SKU.",
        AdminCatalogOutcome.SkuTaken => "Another variant already uses this SKU.",
        AdminCatalogOutcome.Changed when result.CurrentStock is { } stock =>
            $"The stock changed to {stock} while you were editing (a sale, or another admin). Check it and save again.",
        _ => "Someone changed this after you opened it. Check it and save again.",
    };

    public static string For(VoucherAdminOutcome outcome) => outcome switch
    {
        VoucherAdminOutcome.MissingCode => "Enter a code.",
        VoucherAdminOutcome.CodeTaken => "Another voucher already uses this code.",
        VoucherAdminOutcome.InvalidValue => "The discount must be more than 0.",
        VoucherAdminOutcome.InvalidPercent => "A percent discount is at most 100.",
        VoucherAdminOutcome.InvalidMinimum => "The minimum order cannot be below 0.",
        VoucherAdminOutcome.InvalidWindow => "The end must be after the start.",
        VoucherAdminOutcome.InvalidLimit => "The limit is at least 1, or empty for no limit.",
        VoucherAdminOutcome.LimitBelowUsed => "The limit cannot be below the uses already made.",
        VoucherAdminOutcome.UnknownProduct => "Pick products from the list.",
        VoucherAdminOutcome.MissingName => "Enter a name for the promotion.",
        VoucherAdminOutcome.PromotionPercent => "A promotion takes off less than 100%, so nothing is given away.",
        VoucherAdminOutcome.CodeLocked => "This voucher has been used, so its code cannot change (orders keep it). Make a new voucher instead.",
        _ => "Someone changed this voucher after you opened it. Check it and save again.",
    };
}
