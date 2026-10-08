namespace AppleStore.Infrastructure.Payments;

// A stand-in for VNPay and MoMo that lives inside this app (/PaymentSimulator).
// It signs with a key made when the app starts, so the result it sends back
// goes through the same checks a real gateway's would.
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    public const string Path = "/PaymentSimulator";

    public string CreatePaymentUrl(PaymentRequest request) => throw new NotImplementedException();

    public bool Verify(IReadOnlyDictionary<string, string> fields) => throw new NotImplementedException();

    // Used by the simulator page to sign the result it sends back.
    public Dictionary<string, string> Sign(Dictionary<string, string> fields) => throw new NotImplementedException();
}
