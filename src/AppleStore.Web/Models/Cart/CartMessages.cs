using AppleStore.Infrastructure.Services;

namespace AppleStore.Web.Models.Cart;

// The one place a cart result becomes the sentence the shopper reads.
public static class CartMessages
{
    public const string StatusKey = "CartStatus";
    public const string ErrorKey = "CartError";
    public const string Failed = "We could not update your cart. Please try again.";

    public static string Added => "Added to your cart.";
    public static string Updated => "Your cart was updated.";
    public static string Removed => "Removed from your cart.";

    public static string ForAdd(CartResult result) => result.Outcome switch
    {
        CartOutcome.NotFound => "That product could not be found.",
        CartOutcome.ExceedsStock when result.Available > 0 =>
            $"Only {result.Available} more can be added: that is all the stock we have.",
        CartOutcome.ExceedsStock => "Your cart already holds all the stock we have.",
        _ => Common(result.Outcome),
    };

    public static string ForChange(CartResult result) => result.Outcome switch
    {
        CartOutcome.NotFound => "That item is no longer in your cart.",
        CartOutcome.ExceedsStock => $"Only {result.Available} in stock.",
        _ => Common(result.Outcome),
    };

    private static string Common(CartOutcome outcome) => outcome switch
    {
        CartOutcome.InvalidQuantity => "Choose a quantity of at least 1.",
        CartOutcome.Unavailable => "This product is no longer on sale.",
        CartOutcome.NoPrice => "This product has no price yet, so it cannot be ordered.",
        CartOutcome.OutOfStock => "This product is out of stock.",
        _ => Failed,
    };

    public static string? ForLine(CartLineProblem problem, int stock) => problem switch
    {
        CartLineProblem.Unavailable => "No longer on sale",
        CartLineProblem.NoPrice => "No price yet",
        CartLineProblem.OutOfStock => "Out of stock",
        CartLineProblem.NotEnoughStock => $"Only {stock} in stock: lower the quantity",
        _ => null,
    };
}
