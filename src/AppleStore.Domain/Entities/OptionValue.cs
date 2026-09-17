namespace AppleStore.Domain.Entities;

public class OptionValue
{
    public int Id { get; set; }
    public int OptionTypeId { get; set; }
    public string Value { get; set; } = string.Empty;

    public OptionType OptionType { get; set; } = null!;
}
