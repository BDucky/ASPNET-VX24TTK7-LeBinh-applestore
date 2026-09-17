namespace AppleStore.Domain.Entities;

public class CompareList
{
    public int Id { get; set; }
    public int UserId { get; set; }

    public User User { get; set; } = null!;
}
