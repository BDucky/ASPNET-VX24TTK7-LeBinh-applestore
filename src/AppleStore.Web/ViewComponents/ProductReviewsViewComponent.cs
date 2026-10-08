using AppleStore.Domain.Entities;
using AppleStore.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AppleStore.Web.ViewComponents;

// The review block on a product's pages (model and configuration): the
// visible reviews with their average for everyone, and the form only for a
// customer with a delivered order of the product.
public class ProductReviewsViewComponent : ViewComponent
{
    private readonly IReviewService _reviews;
    private readonly UserManager<User> _users;

    public ProductReviewsViewComponent(IReviewService reviews, UserManager<User> users)
    {
        _reviews = reviews;
        _users = users;
    }

    public async Task<IViewComponentResult> InvokeAsync(int productId)
    {
        var reviews = await _reviews.ForProductAsync(productId, HttpContext.RequestAborted);
        (bool CanReview, ReviewView? Mine)? mine = _users.GetUserId(UserClaimsPrincipal) is { } id
            ? await _reviews.MineAsync(int.Parse(id), productId, HttpContext.RequestAborted)
            : null;
        return View(new ProductReviewsBlock(productId, reviews, mine?.CanReview, mine?.Mine,
            HttpContext.Request.Path + HttpContext.Request.QueryString));
    }
}

public sealed record ProductReviewsBlock(int ProductId, ProductReviews Reviews, bool? CanReview, ReviewView? Mine, string ReturnUrl);
