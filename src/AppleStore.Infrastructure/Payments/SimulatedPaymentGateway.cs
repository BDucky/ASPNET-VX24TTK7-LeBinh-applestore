using System.Globalization;
using System.Security.Cryptography;

namespace AppleStore.Infrastructure.Payments;

// A stand-in for VNPay and MoMo that lives inside this app (/PaymentSimulator).
// It signs with a key made when the app starts, so the result it sends back
// goes through the same checks a real gateway's would, and a link signed by
// an earlier run of the app no longer verifies.
public sealed class SimulatedPaymentGateway : IPaymentGateway
{
    public const string Path = "/PaymentSimulator";

    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);

    public string CreatePaymentUrl(PaymentRequest request)
    {
        var fields = Sign(new Dictionary<string, string>
        {
            ["paymentId"] = request.PaymentId.ToString(CultureInfo.InvariantCulture),
            ["orderId"] = request.OrderId.ToString(CultureInfo.InvariantCulture),
            ["amount"] = request.Amount.ToString("0.##", CultureInfo.InvariantCulture),
            ["method"] = request.Method.ToString(),
        });
        return Path + "?" + string.Join("&", fields.Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}"));
    }

    public bool Verify(IReadOnlyDictionary<string, string> fields) => PaymentSignature.Verify(fields, _key);

    // Used by the simulator page to sign the result it sends back.
    public Dictionary<string, string> Sign(Dictionary<string, string> fields)
    {
        fields[PaymentSignature.Field] = PaymentSignature.Sign(fields, _key);
        return fields;
    }
}
