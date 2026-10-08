using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Cart;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.Controllers;

// Use case 32: a customer posts or edits their review from the product page
// and comes back to it with a message.
[Authorize]
[Route("Reviews")]
public class ReviewsController : Controller
{
    public const string NotPurchased = "You can review this after your order with it is delivered.";

    private readonly IReviewService _reviews;
    private readonly UserManager<User> _users;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(IReviewService reviews, UserManager<User> users, ILogger<ReviewsController> logger)
    {
        _reviews = reviews;
        _users = users;
        _logger = logger;
    }

    [HttpPost("{productId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int productId, int? rating, string? content, string? returnUrl, CancellationToken ct)
    {
        ReviewOutcome outcome;
        try
        {
            outcome = await _reviews.SubmitAsync(int.Parse(_users.GetUserId(User)!), productId, rating ?? 0, content, ct);
        }
        catch (Exception ex) when (ex.IsDatabaseFailure())
        {
            _logger.LogError(ex, "Saving a review of product {ProductId} failed", productId);
            outcome = ReviewOutcome.NotFound;
        }

        TempData[outcome == ReviewOutcome.Done ? CartMessages.StatusKey : CartMessages.ErrorKey] = outcome switch
        {
            ReviewOutcome.Done => "Thank you for your review.",
            ReviewOutcome.InvalidRating => "Choose from 1 to 5 stars.",
            ReviewOutcome.NotPurchased => NotPurchased,
            _ => "We could not save your review. Please try again.",
        };
        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
