using AppleStore.Infrastructure.Payments;

namespace AppleStore.Tests;

public class PaymentSignatureTests
{
    private static readonly byte[] Key = "0123456789abcdef0123456789abcdef"u8.ToArray();

    private static Dictionary<string, string> Signed(Dictionary<string, string> fields, byte[]? key = null)
    {
        fields[PaymentSignature.Field] = PaymentSignature.Sign(fields, key ?? Key);
        return fields;
    }

    private static Dictionary<string, string> Fields() => new()
    {
        ["paymentId"] = "7",
        ["amount"] = "27300000",
        ["result"] = "00",
        ["note"] = "Thanh toán đơn #1 & more",
    };

    [Fact]
    public void A_signed_set_of_fields_verifies()
    {
        Assert.True(PaymentSignature.Verify(Signed(Fields()), Key));
    }

    [Fact]
    public void The_order_fields_arrive_in_does_not_matter()
    {
        var reversed = Fields().Reverse().ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal(PaymentSignature.Sign(Fields(), Key), PaymentSignature.Sign(reversed, Key));
    }

    [Theory]
    [InlineData("amount", "1")]
    [InlineData("result", "24")]
    [InlineData("paymentId", "8")]
    public void A_changed_field_fails(string field, string value)
    {
        var fields = Signed(Fields());
        fields[field] = value;

        Assert.False(PaymentSignature.Verify(fields, Key));
    }

    [Fact]
    public void An_added_field_fails()
    {
        var fields = Signed(Fields());
        fields["extra"] = "1";

        Assert.False(PaymentSignature.Verify(fields, Key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-hex")]
    [InlineData("abcd")]
    public void A_missing_or_malformed_signature_fails(string? signature)
    {
        var fields = Fields();
        if (signature is not null)
            fields[PaymentSignature.Field] = signature;

        Assert.False(PaymentSignature.Verify(fields, Key));
    }

    [Fact]
    public void Another_key_fails()
    {
        Assert.False(PaymentSignature.Verify(Signed(Fields(), "another key, another key, abcdef"u8.ToArray()), Key));
    }

    [Fact]
    public void The_signature_is_hex_in_either_case()
    {
        var fields = Signed(Fields());
        fields[PaymentSignature.Field] = fields[PaymentSignature.Field].ToUpperInvariant();

        Assert.True(PaymentSignature.Verify(fields, Key));
    }
}
