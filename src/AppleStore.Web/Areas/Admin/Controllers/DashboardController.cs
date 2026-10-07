using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Web.Areas.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Web.Areas.Admin.Controllers;

// The Admin area's landing page (/Admin). The role check comes from the
// role claim AppUserClaimsPrincipalFactory writes at sign-in; a visitor is
// sent to sign in, any other role to /Account/AccessDenied.
[Area("Admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new DashboardViewModel(
            Products: await _db.Products.CountAsync(ct),
            ProductsOnSale: await _db.Products.CountAsync(p => p.Status, ct),
            Customers: await _db.Users.CountAsync(u => u.Role == UserRole.Customer, ct),
            Orders: await _db.Orders.CountAsync(ct));
        return View(model);
    }
}
