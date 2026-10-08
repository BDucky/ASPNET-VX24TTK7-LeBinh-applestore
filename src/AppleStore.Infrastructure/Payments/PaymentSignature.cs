namespace AppleStore.Infrastructure.Payments;

// Signs and checks the fields a payment gateway sends back, the way VNPay
// does: fields sorted by name, joined as a query string, HMAC-SHA512 in hex.
// The "signature" field itself is never part of what is signed.
public static class PaymentSignature
{
    public const string Field = "signature";

    public static string Sign(IReadOnlyDictionary<string, string> fields, byte[] key) => throw new NotImplementedException();

    public static bool Verify(IReadOnlyDictionary<string, string> fields, byte[] key) => throw new NotImplementedException();
}
