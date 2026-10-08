using AppleStore.Domain.Enums;

namespace AppleStore.Infrastructure.Payments;

// What the gateway needs to take one payment attempt.
public sealed record PaymentRequest(int PaymentId, int OrderId, decimal Amount, PaymentMethod Method);

// Result codes the gateway sends back, VNPay's numbers.
public static class PaymentResultCode
{
    public const string Success = "00";
    public const string Cancelled = "24";
    public const string Failed = "51";
}

public enum PayStartOutcome
{
    Ready,
    NotFound,
    NotOnline,
    AlreadyPaid,
    Unavailable,
}

public sealed record PayStart(PayStartOutcome Outcome, string? RedirectUrl = null);

public enum CallbackOutcome
{
    Paid,
    AlreadyPaid,
    Failed,
    Cancelled,
    // Money was taken twice for one order: recorded, and the shopper is told
    // a refund is due.
    PaidTwice,
    Rejected,
}

public sealed record CallbackResult(CallbackOutcome Outcome, int? OrderId = null);
