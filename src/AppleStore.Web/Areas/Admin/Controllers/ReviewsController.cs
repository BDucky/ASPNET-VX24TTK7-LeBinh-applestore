using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Controllers;
using AppleStore.Web.Models.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Areas.Admin.Controllers;

// Staff reply to reviews and hide or show them (the report's "remove").
[Area("Admin")]
[Authorize(Roles = $"{nameof(UserRole.Admin)},{nameof(UserRole.Employee)}")]
[Route("Admin/Reviews")]
public class ReviewsController : Controller
{
    private readonly IReviewService _reviews;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(IReviewService reviews, ILogger<ReviewsController> logger)
    {
        _reviews = reviews;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _reviews.ListForStaffAsync(ct));

    [HttpPost("{id:int}/Reply"), ValidateAntiForgeryToken]
    public Task<IActionResult> Reply(int id, string? reply, CancellationToken ct) =>
        RunAsync(() => _reviews.ReplyAsync(id, reply, ct), "Reply saved.");

    [HttpPost("{id:int}/Hide"), ValidateAntiForgeryToken]
    public Task<IActionResult> Hide(int id, CancellationToken ct) =>
        RunAsync(() => _reviews.SetVisibleAsync(id, false, ct), "Review hidden from the shop.");

    [HttpPost("{id:int}/Show"), ValidateAntiForgeryToken]
    public Task<IActionResult> Show(int id, CancellationToken ct) =>
        RunAsync(() => _reviews.SetVisibleAsync(id, true, ct), "Review shown in the shop again.");

    private async Task<IActionResult> RunAsync(Func<Task<ReviewOutcome>> change, string done)
    {
        ReviewOutcome outcome;
        try
        {
            outcome = await change();
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Changing a review failed");
            TempData[CartMessages.ErrorKey] = "We could not save that. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        if (outcome == ReviewOutcome.NotFound)
            return NotFound();
        TempData[outcome == ReviewOutcome.Done ? CartMessages.StatusKey : CartMessages.ErrorKey] =
            outcome == ReviewOutcome.Done ? done : "Write a reply first.";
        return RedirectToAction(nameof(Index));
    }
}
