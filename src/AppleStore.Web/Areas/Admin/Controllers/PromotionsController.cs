using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Areas.Admin.Controllers;

// Use case 28: automatic promotions. The report gives promotions to
// employees as well as admins, so both roles may use this page.
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Employee)}")]
[Route("Admin/Promotions")]
public class PromotionsController(IAdminVoucherService vouchers, AppDbContext db, ILogger<PromotionsController> logger)
    : DiscountAdminController(vouchers, db, logger)
{
    protected override VoucherKind Kind => VoucherKind.Automatic;
}
