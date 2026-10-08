using AppleStore.Infrastructure.Data;

namespace AppleStore.Infrastructure.Services;

public class ReviewService : IReviewService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public ReviewService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<ProductReviews> ForProductAsync(int productId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<(bool CanReview, ReviewView? Mine)> MineAsync(int userId, int productId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ReviewOutcome> SubmitAsync(int userId, int productId, int rating, string? content, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<ReviewView>> ListForStaffAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ReviewOutcome> ReplyAsync(int reviewId, string? reply, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<ReviewOutcome> SetVisibleAsync(int reviewId, bool visible, CancellationToken ct = default) => throw new NotImplementedException();
}
