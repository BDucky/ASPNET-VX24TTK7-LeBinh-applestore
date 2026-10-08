using AppleStore.Infrastructure.Services;
using AppleStore.Web.Models.Cart;

namespace AppleStore.Web.Models;

// The sentence the shopper reads after a compare action. It uses the cart's
// notice keys so the one notice in the layout shows it.
public static class CompareMessages
{
    public const string StatusKey = CartMessages.StatusKey;
    public const string ErrorKey = CartMessages.ErrorKey;
    public const string Failed = "We could not update your compare list. Please try again.";

    public static string? Error(CompareOutcome outcome) => outcome switch
    {
        CompareOutcome.Full => $"You can compare up to {CompareService.MaxItems} products. Remove one first.",
        CompareOutcome.NotFound => "That product could not be found.",
        _ => null,
    };

    public static string Status(CompareOutcome outcome) =>
        outcome == CompareOutcome.AlreadyIn ? "Already in your compare list." : "Added to compare.";
}
