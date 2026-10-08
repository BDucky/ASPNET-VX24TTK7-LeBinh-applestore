using System.Globalization;

namespace AppleStore.Web.Services;

// The compare list as product ids in one cookie, "12.5.7", first added
// first (owner's decision 2026-10-08: a visitor can compare without an
// account). Anything in it that is not a positive id is skipped; which ids
// still count is CompareService's call. No expiry date: it ends with the
// browser session, so no lifetime had to be picked.
public static class CompareCookie
{
    public const string Name = "AppleStore.Compare";

    public static IReadOnlyList<int> Read(HttpRequest request) =>
        (request.Cookies[Name] ?? "")
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

    public static void Write(HttpContext context, IReadOnlyList<int> ids)
    {
        if (ids.Count == 0)
        {
            context.Response.Cookies.Delete(Name);
            return;
        }
        context.Response.Cookies.Append(Name, string.Join('.', ids), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
        });
    }
}
