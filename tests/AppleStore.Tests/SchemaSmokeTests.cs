namespace AppleStore.Tests;

public class SchemaSmokeTests
{
    [Fact]
    public void Schema_creates_cleanly_from_all_24_entity_configurations()
    {
        using var fixture = new SqliteInMemoryFixture();

        var created = fixture.Context.Database.EnsureCreated();

        Assert.True(created);
    }
}
