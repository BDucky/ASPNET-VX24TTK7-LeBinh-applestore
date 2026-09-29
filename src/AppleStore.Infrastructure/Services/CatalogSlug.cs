using System.Globalization;
using System.Text;

namespace AppleStore.Infrastructure.Services;

// The one rule for turning a catalog name into a URL segment, used for
// configuration pages: "iPhone 18 Pro Max 256GB ( Mỹ )" becomes
// "iphone-18-pro-max-256gb-my". Vietnamese marks are dropped, and "đ",
// which has no decomposed form, is mapped to "d" by hand.
public static class CatalogSlug
{
    public static string From(string name)
    {
        var decomposed = name.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder(decomposed.Length);
        var pendingDash = false;
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsAsciiLetterOrDigit(ch))
            {
                if (pendingDash && slug.Length > 0)
                    slug.Append('-');
                slug.Append(char.ToLowerInvariant(ch));
                pendingDash = false;
            }
            else
            {
                pendingDash = true;
            }
        }
        return slug.ToString();
    }
}
