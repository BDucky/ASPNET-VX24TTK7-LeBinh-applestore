using AppleStore.Domain.Enums;

namespace AppleStore.Domain.Entities;

public class ReviewMedia
{
    public int Id { get; set; }
    public int ReviewId { get; set; }
    public string MediaUrl { get; set; } = string.Empty;
    public ReviewMediaType MediaType { get; set; }
    public int SortOrder { get; set; }

    public Review Review { get; set; } = null!;
}
