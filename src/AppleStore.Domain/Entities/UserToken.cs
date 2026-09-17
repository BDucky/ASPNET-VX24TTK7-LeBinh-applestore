using AppleStore.Domain.Enums;

namespace AppleStore.Domain.Entities;

public class UserToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public UserTokenType Type { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiredAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
