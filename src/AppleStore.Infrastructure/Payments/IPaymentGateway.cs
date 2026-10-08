namespace AppleStore.Infrastructure.Payments;

// An online payment gateway (VNPay, MoMo). Only the simulated one exists
// (decided 2026-10-08: no sandbox credentials); a real one would sign with
// the provider's secret instead.
public interface IPaymentGateway
{
    // Where to send the shopper to pay this attempt.
    string CreatePaymentUrl(PaymentRequest request);

    // True when the fields came from this gateway unchanged.
    bool Verify(IReadOnlyDictionary<string, string> fields);
}
