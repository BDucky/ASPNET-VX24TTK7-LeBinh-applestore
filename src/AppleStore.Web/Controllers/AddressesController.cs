using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

// The signed-in customer's delivery addresses (use case 6, "manage delivery
// addresses"). ProfileService only ever looks an address up among the
// user's own, so another user's id is a 404 here.
[Authorize]
[Route("Account/Addresses")]
public class AddressesController : Controller
{
    private readonly IProfileService _profile;
    private readonly UserManager<User> _users;

    public AddressesController(IProfileService profile, UserManager<User> users)
    {
        _profile = profile;
        _users = users;
    }

    private int UserId => int.Parse(_users.GetUserId(User)!);

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _profile.GetAddressesAsync(UserId, ct));

    [HttpGet("Create")]
    public IActionResult Create() => View("Form", new AddressViewModel());

    [HttpPost("Create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AddressViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Form", model);

        await _profile.AddAddressAsync(UserId, ToInput(model), ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var address = await _profile.GetAddressAsync(UserId, id, ct);
        if (address is null)
            return NotFound();

        ViewData["AddressId"] = id;
        return View("Form", new AddressViewModel
        {
            Label = address.Label,
            FullName = address.FullName,
            Phone = address.Phone,
            AddressLine = address.AddressLine,
            Ward = address.Ward,
            District = address.District,
            City = address.City,
            IsDefault = address.IsDefault,
        });
    }

    [HttpPost("Edit/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AddressViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            ViewData["AddressId"] = id;
            return View("Form", model);
        }

        return await _profile.UpdateAddressAsync(UserId, id, ToInput(model), ct)
            ? RedirectToAction(nameof(Index))
            : NotFound();
    }

    [HttpPost("Delete/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await _profile.DeleteAddressAsync(UserId, id, ct) ? RedirectToAction(nameof(Index)) : NotFound();

    [HttpPost("SetDefault/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct) =>
        await _profile.SetDefaultAddressAsync(UserId, id, ct) ? RedirectToAction(nameof(Index)) : NotFound();

    private static AddressInput ToInput(AddressViewModel m) =>
        new(m.Label, m.FullName, m.Phone, m.AddressLine, m.Ward, m.District, m.City, m.IsDefault);
}
