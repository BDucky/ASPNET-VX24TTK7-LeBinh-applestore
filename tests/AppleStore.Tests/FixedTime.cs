namespace AppleStore.Tests;

// A clock that always reads the same instant, for TimeProvider-based services.
public sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
