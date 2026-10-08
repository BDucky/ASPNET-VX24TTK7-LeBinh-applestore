namespace AppleStore.Infrastructure.Services;

public sealed record ReviewView(int Id, int ProductId, string ProductName, string Author, int Rating, string? Content,
    DateTime CreatedAt, DateTime? UpdatedAt, string? Reply, DateTime? RepliedAt, bool Visible);

// Average and count cover visible reviews only.
public sealed record ProductReviews(double? Average, int Count, IReadOnlyList<ReviewView> Reviews);

public enum ReviewOutcome
{
    Done,
    NotFound,
    // Only a customer with a delivered order containing the product may review it.
    NotPurchased,
    InvalidRating,
    MissingReply,
}

public interface IReviewService
{
    Task<ProductReviews> ForProductAsync(int productId, CancellationToken ct = default);

    // Whether this customer may review the product, and their review if they wrote one.
    Task<(bool CanReview, ReviewView? Mine)> MineAsync(int userId, int productId, CancellationToken ct = default);

    // Writes the customer's review, or edits it if they already wrote one.
    Task<ReviewOutcome> SubmitAsync(int userId, int productId, int rating, string? content, CancellationToken ct = default);

    Task<IReadOnlyList<ReviewView>> ListForStaffAsync(CancellationToken ct = default);
    Task<ReviewOutcome> ReplyAsync(int reviewId, string? reply, CancellationToken ct = default);
    Task<ReviewOutcome> SetVisibleAsync(int reviewId, bool visible, CancellationToken ct = default);
}
