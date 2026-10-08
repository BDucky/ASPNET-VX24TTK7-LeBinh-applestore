using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Controllers;
using AppleStore.Web.Models.Admin;
using AppleStore.Web.Models.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Areas.Admin.Controllers;

// Accounts and their roles. The person's open sessions end when their role
// changes (new security stamp, checked on every request).
[Area("Admin")]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("Admin/Users")]
public class UsersController : Controller
{
    private readonly IAdminUserService _accounts;
    private readonly UserManager<User> _users;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IAdminUserService accounts, UserManager<User> users, ILogger<UsersController> logger)
    {
        _accounts = accounts;
        _users = users;
        _logger = logger;
    }

    private int UserId => int.Parse(_users.GetUserId(User)!);

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search, CancellationToken ct) =>
        View(new UsersPage(await _accounts.ListAsync(search, ct), search, UserId));

    [HttpPost("{id:int}/Role"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Role(int id, UserRole role, CancellationToken ct)
    {
        RoleChangeOutcome outcome;
        try
        {
            outcome = await _accounts.ChangeRoleAsync(UserId, id, role, ct);
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Changing the role of account {UserId} failed", id);
            TempData[CartMessages.ErrorKey] = AdminMessages.SaveFailed;
            return RedirectToAction(nameof(Index));
        }

        if (outcome == RoleChangeOutcome.NotFound)
            return NotFound();
        TempData[outcome == RoleChangeOutcome.Done ? CartMessages.StatusKey : CartMessages.ErrorKey] =
            outcome == RoleChangeOutcome.Done ? AdminMessages.RoleChanged : AdminMessages.OwnRole;
        return RedirectToAction(nameof(Index));
    }
}
