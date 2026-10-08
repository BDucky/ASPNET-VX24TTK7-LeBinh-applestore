using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AppleStore.Infrastructure.Services;

// Use case 32. A review needs a verified purchase: a delivered (Completed)
// order of this customer that contains the product. One review per customer
// per product (unique index); writing again edits it. Staff reply and hide;
// hiding is the report's "remove", and an author's edit never unhides.
public class ReviewService : IReviewService
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public ReviewService(AppDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    private IQueryable<ReviewView> Views(IQueryable<Review> reviews) =>
        reviews.OrderByDescending(r => r.Id).Select(r => new ReviewView(r.Id, r.ProductId, r.Product.Name, r.User.FullName, r.Rating, r.Content,
            r.CreatedAt, r.UpdatedAt, r.Reply, r.RepliedAt, r.Status));

    public async Task<ProductReviews> ForProductAsync(int productId, CancellationToken ct = default)
    {
        var visible = await Views(_db.Reviews.AsNoTracking().Where(r => r.ProductId == productId && r.Status)).ToListAsync(ct);
        return new ProductReviews(visible.Count == 0 ? null : Math.Round(visible.Average(r => r.Rating), 1), visible.Count, visible);
    }

    public async Task<(bool CanReview, ReviewView? Mine)> MineAsync(int userId, int productId, CancellationToken ct = default)
    {
        var mine = await Views(_db.Reviews.AsNoTracking().Where(r => r.ProductId == productId && r.UserId == userId)).FirstOrDefaultAsync(ct);
        return (await BoughtAsync(userId, productId, ct), mine);
    }

    private Task<bool> BoughtAsync(int userId, int productId, CancellationToken ct) =>
        _db.OrderItems.AnyAsync(i => i.ProductId == productId && i.Order.UserId == userId && i.Order.Status == OrderStatus.Completed, ct);

    public async Task<ReviewOutcome> SubmitAsync(int userId, int productId, int rating, string? content, CancellationToken ct = default)
    {
        if (rating is < 1 or > 5)
            return ReviewOutcome.InvalidRating;
        if (!await BoughtAsync(userId, productId, ct))
            return ReviewOutcome.NotPurchased;

        var text = string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        var now = _time.GetUtcNow().UtcDateTime;
        // Editing leaves Status alone, so a hidden review stays hidden.
        var edited = await _db.Reviews.Where(r => r.ProductId == productId && r.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Rating, rating).SetProperty(r => r.Content, text).SetProperty(r => r.UpdatedAt, now), ct);
        if (edited == 1)
            return ReviewOutcome.Done;

        _db.Reviews.Add(new Review
        {
            ProductId = productId,
            UserId = userId,
            Rating = rating,
            Content = text,
            PurchaseVerified = true,
            Status = true,
            CreatedAt = now,
        });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same customer's other tab created it a moment earlier: edit that one.
            _db.ChangeTracker.Clear();
            await _db.Reviews.Where(r => r.ProductId == productId && r.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Rating, rating).SetProperty(r => r.Content, text).SetProperty(r => r.UpdatedAt, now), ct);
        }
        return ReviewOutcome.Done;
    }

    public async Task<IReadOnlyList<ReviewView>> ListForStaffAsync(CancellationToken ct = default) =>
        await Views(_db.Reviews.AsNoTracking()).ToListAsync(ct);

    public async Task<ReviewOutcome> ReplyAsync(int reviewId, string? reply, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reply))
            return ReviewOutcome.MissingReply;
        var now = _time.GetUtcNow().UtcDateTime;
        var saved = await _db.Reviews.Where(r => r.Id == reviewId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Reply, reply.Trim()).SetProperty(r => r.RepliedAt, now), ct);
        return saved == 1 ? ReviewOutcome.Done : ReviewOutcome.NotFound;
    }

    public async Task<ReviewOutcome> SetVisibleAsync(int reviewId, bool visible, CancellationToken ct = default)
    {
        var saved = await _db.Reviews.Where(r => r.Id == reviewId).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, visible), ct);
        return saved == 1 ? ReviewOutcome.Done : ReviewOutcome.NotFound;
    }
}
