using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Areas.Admin.Controllers;

// Use cases 29-31: vouchers typed at checkout, for admins.
[Area("Admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("Admin/Vouchers")]
public class VouchersController(IAdminVoucherService vouchers, AppDbContext db, ILogger<VouchersController> logger)
    : DiscountAdminController(vouchers, db, logger)
{
    protected override VoucherKind Kind => VoucherKind.Code;
}
