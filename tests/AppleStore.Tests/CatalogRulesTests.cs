using AppleStore.Infrastructure.Services;
using AppleStore.Web.Formatting;

namespace AppleStore.Tests;

public class CatalogRulesTests
{
    [Theory]
    [InlineData("iPhone 18 Pro Max 256GB ( VN )", "iphone-18-pro-max-256gb-vn")]
    [InlineData("iPhone 18 Pro Max 2TB ( Mỹ )", "iphone-18-pro-max-2tb-my")]
    [InlineData("Dây Cao Su - Sport Band ( Chính hãng )", "day-cao-su-sport-band-chinh-hang")]
    [InlineData("iPad Pro M5 13\" 2TB WIFI", "ipad-pro-m5-13-2tb-wifi")]
    [InlineData("Macbook Neo 8GB/256GB - Touch ID", "macbook-neo-8gb-256gb-touch-id")]
    public void CatalogSlug_turns_a_configuration_name_into_its_url_segment(string name, string slug)
    {
        Assert.Equal(slug, CatalogSlug.From(name));
    }

    private static ConfigurationDetail Config(params VariantChoice[] choices) =>
        new(1, "iPad Mini 7", "ipad-mini-7", "iPad", "ipad", null, "iPad Mini Gen 7 WIFI - 128GB", "ipad-mini-gen-7-wifi-128gb", choices);

    [Fact]
    public void Select_prefers_an_exact_colour_and_region_match()
    {
        var config = Config(new("A", "Blue", "VN", 1m, 1), new("B", "Blue", "Mỹ", 2m, 1));

        Assert.Equal("B", config.Select("blue", "Mỹ").SKU);
    }

    [Fact]
    public void Select_falls_back_to_the_first_choice_in_that_colour()
    {
        var config = Config(new("A", "Purple", "VN", 1m, 1), new("B", "Blue", "VN", 2m, 1), new("C", "Blue", "Mỹ", 2m, 1));

        Assert.Equal("B", config.Select("Blue", "Nowhere").SKU);
    }

    [Fact]
    public void Select_opens_on_the_first_choice_when_nothing_matches()
    {
        var config = Config(new("A", "Purple", "VN", 1m, 1), new("B", "Blue", "VN", 2m, 1));

        Assert.Equal("A", config.Select(null, null).SKU);
        Assert.Equal("A", config.Select("Green", null).SKU);
    }

    [Theory]
    [InlineData(42_300_000, "42.300.000 VNĐ")]
    [InlineData(750_000, "750.000 VNĐ")]
    [InlineData(250_000.4, "250.000 VNĐ")]
    public void PriceText_formats_vnd_the_way_rauvang_prints_it(double price, string text)
    {
        Assert.Equal(text, PriceText.Vnd((decimal)price));
    }

    [Fact]
    public void PriceText_shows_contact_for_price_when_there_is_no_price()
    {
        Assert.Equal("Contact for price", PriceText.Vnd(null));
    }
}
