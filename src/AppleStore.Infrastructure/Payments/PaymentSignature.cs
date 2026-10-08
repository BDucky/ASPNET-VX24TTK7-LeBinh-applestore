using System.Security.Cryptography;
using System.Text;

namespace AppleStore.Infrastructure.Payments;

// Signs and checks the fields a payment gateway sends back, the way VNPay
// does: fields sorted by name, joined as a query string, HMAC-SHA512 in hex.
// The "signature" field itself is never part of what is signed.
public static class PaymentSignature
{
    public const string Field = "signature";

    public static string Sign(IReadOnlyDictionary<string, string> fields, byte[] key) =>
        Convert.ToHexStringLower(Hash(fields, key));

    public static bool Verify(IReadOnlyDictionary<string, string> fields, byte[] key)
    {
        if (!fields.TryGetValue(Field, out var given) || string.IsNullOrEmpty(given))
            return false;

        byte[] givenBytes;
        try
        {
            givenBytes = Convert.FromHexString(given);
        }
        catch (FormatException)
        {
            return false;
        }

        // Constant time, so the comparison does not leak how much matched.
        return CryptographicOperations.FixedTimeEquals(givenBytes, Hash(fields, key));
    }

    private static byte[] Hash(IReadOnlyDictionary<string, string> fields, byte[] key)
    {
        var data = string.Join("&", fields
            .Where(f => f.Key != Field)
            .OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}"));
        return HMACSHA512.HashData(key, Encoding.UTF8.GetBytes(data));
    }
}
