using AppleStore.Domain.Entities;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Data;
using AppleStore.Infrastructure.Services;
using static AppleStore.Tests.ProductCatalogServiceTests;

namespace AppleStore.Tests;

// ReviewService on SQLite with orders in the states the shop produces, so the
// "verified purchase" rule is checked against real order rows.
public sealed class ReviewServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);

    private readonly ShopTestDb _shop = new();
    private readonly AppDbContext _db;
    private readonly ReviewService _sut;
    private readonly int _alice;
    private readonly int _bob;
    private readonly int _phone;
    private readonly int _pods;
    private readonly ProductVariant _phoneVariant;

    public ReviewServiceTests()
    {
        _db = _shop.Context();
        var alice = ShopTestDb.NewUser("alice@example.com");
        alice.FullName = "Alice Nguyen";
        var bob = ShopTestDb.NewUser("bob@example.com");
        bob.FullName = "Bob Tran";
        var phone = NewProduct(NewCategory("iPhone", "iphone"), "iPhone 17", "iphone-17", 1m);
        var pods = NewProduct(NewCategory("AirPods", "airpods"), "AirPods Pro 3", "airpods-pro-3", 1m);
        _phoneVariant = AddVariant(_db, phone, "P", 1_000m, 5);
        _db.AddRange(alice, bob, pods);
        _db.SaveChanges();
        (_alice, _bob, _phone, _pods) = (alice.Id, bob.Id, phone.Id, pods.Id);
        _sut = new ReviewService(_db, new FixedTime(Now));
    }

    public void Dispose()
    {
        _db.Dispose();
        _shop.Dispose();
    }

    private void Bought(int userId, OrderStatus status)
    {
        var order = new Order
        {
            UserId = userId,
            Status = status,
            PaymentStatus = OrderPaymentStatus.Paid,
            Subtotal = 1_000m,
            TotalAmount = 1_000m,
            ReceiverName = "R",
            Phone = "0900000000",
            AddressLine = "1 Street",
            CreatedAt = Now.UtcDateTime,
            UpdatedAt = Now.UtcDateTime,
        };
        _db.Orders.Add(order);
        _db.OrderItems.Add(new OrderItem { Order = order, ProductId = _phone, VariantId = _phoneVariant.Id, Price = 1_000m, Quantity = 1 });
        _db.SaveChanges();
    }

    [Fact]
    public async Task A_customer_with_a_delivered_order_reviews_the_product_and_it_shows()
    {
        Bought(_alice, OrderStatus.Completed);

        Assert.True((await _sut.MineAsync(_alice, _phone)).CanReview);
        Assert.Equal(ReviewOutcome.Done, await _sut.SubmitAsync(_alice, _phone, 5, "  Great phone  "));

        var reviews = await _sut.ForProductAsync(_phone);
        var review = Assert.Single(reviews.Reviews);
        Assert.Equal(("Alice Nguyen", 5, "Great phone", Now.UtcDateTime), (review.Author, review.Rating, review.Content, review.CreatedAt));
        Assert.Equal((5.0, 1), (reviews.Average, reviews.Count));
        Assert.True(_db.Reviews.Single().PurchaseVerified);
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Shipping)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task An_order_not_delivered_does_not_count_as_a_purchase(OrderStatus status)
    {
        Bought(_alice, status);

        Assert.False((await _sut.MineAsync(_alice, _phone)).CanReview);
        Assert.Equal(ReviewOutcome.NotPurchased, await _sut.SubmitAsync(_alice, _phone, 5, null));
        Assert.Empty(_db.Reviews);
    }

    [Fact]
    public async Task Someone_who_never_bought_it_cannot_review_it()
    {
        Bought(_alice, OrderStatus.Completed);

        Assert.Equal(ReviewOutcome.NotPurchased, await _sut.SubmitAsync(_bob, _phone, 4, null));
        Assert.Equal(ReviewOutcome.NotPurchased, await _sut.SubmitAsync(_alice, _pods, 4, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task A_rating_is_one_to_five(int rating)
    {
        Bought(_alice, OrderStatus.Completed);

        Assert.Equal(ReviewOutcome.InvalidRating, await _sut.SubmitAsync(_alice, _phone, rating, null));
        Assert.Empty(_db.Reviews);
    }

    [Fact]
    public async Task Writing_again_edits_the_one_review()
    {
        Bought(_alice, OrderStatus.Completed);
        await _sut.SubmitAsync(_alice, _phone, 5, "Great");

        Assert.Equal(ReviewOutcome.Done, await _sut.SubmitAsync(_alice, _phone, 3, "Battery is short"));

        var review = Assert.Single((await _sut.ForProductAsync(_phone)).Reviews);
        Assert.Equal((3, "Battery is short", (DateTime?)Now.UtcDateTime), (review.Rating, review.Content, review.UpdatedAt));
        Assert.Equal(3, (await _sut.MineAsync(_alice, _phone)).Mine!.Rating);
    }

    [Fact]
    public async Task The_average_covers_visible_reviews_only()
    {
        Bought(_alice, OrderStatus.Completed);
        Bought(_bob, OrderStatus.Completed);
        await _sut.SubmitAsync(_alice, _phone, 4, null);
        await _sut.SubmitAsync(_bob, _phone, 5, null);
        Assert.Equal((4.5, 2), ((await _sut.ForProductAsync(_phone)).Average, (await _sut.ForProductAsync(_phone)).Count));

        var bobs = (await _sut.ListForStaffAsync()).Single(r => r.Author == "Bob Tran");
        Assert.Equal(ReviewOutcome.Done, await _sut.SetVisibleAsync(bobs.Id, false));

        var after = await _sut.ForProductAsync(_phone);
        Assert.Equal((4.0, 1, "Alice Nguyen"), (after.Average, after.Count, after.Reviews.Single().Author));
    }

    // A hidden review stays hidden when its author edits it.
    [Fact]
    public async Task Editing_a_hidden_review_does_not_show_it_again()
    {
        Bought(_alice, OrderStatus.Completed);
        await _sut.SubmitAsync(_alice, _phone, 1, "rude words");
        await _sut.SetVisibleAsync((await _sut.ListForStaffAsync()).Single().Id, false);

        await _sut.SubmitAsync(_alice, _phone, 1, "rude words again");

        Assert.Empty((await _sut.ForProductAsync(_phone)).Reviews);
        Assert.False((await _sut.ListForStaffAsync()).Single().Visible);
    }

    [Fact]
    public async Task Staff_reply_and_the_reply_shows_under_the_review()
    {
        Bought(_alice, OrderStatus.Completed);
        await _sut.SubmitAsync(_alice, _phone, 4, null);
        var id = (await _sut.ListForStaffAsync()).Single().Id;

        Assert.Equal(ReviewOutcome.MissingReply, await _sut.ReplyAsync(id, "   "));
        Assert.Equal(ReviewOutcome.Done, await _sut.ReplyAsync(id, " Thank you! "));
        Assert.Equal(ReviewOutcome.NotFound, await _sut.ReplyAsync(999_999, "x"));
        Assert.Equal(ReviewOutcome.NotFound, await _sut.SetVisibleAsync(999_999, false));

        var review = Assert.Single((await _sut.ForProductAsync(_phone)).Reviews);
        Assert.Equal(("Thank you!", (DateTime?)Now.UtcDateTime), (review.Reply, review.RepliedAt));
    }

    [Fact]
    public async Task No_reviews_means_no_average()
    {
        var reviews = await _sut.ForProductAsync(_phone);

        Assert.Equal((null, 0), (reviews.Average, reviews.Count));
    }

    [Fact]
    public async Task Staff_see_every_review_newest_first_hidden_included()
    {
        Bought(_alice, OrderStatus.Completed);
        Bought(_bob, OrderStatus.Completed);
        await _sut.SubmitAsync(_alice, _phone, 4, null);
        await _sut.SubmitAsync(_bob, _phone, 5, null);
        await _sut.SetVisibleAsync((await _sut.ListForStaffAsync()).Single(r => r.Author == "Alice Nguyen").Id, false);

        Assert.Equal(["Bob Tran", "Alice Nguyen"], (await _sut.ListForStaffAsync()).Select(r => r.Author));
    }
}
