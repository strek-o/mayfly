using Mayfly.Core.Data;

namespace Mayfly.Core.Time;

public static class Expiration
{
    public static DateTimeOffset? Calculate(Lifetime lifetime, DateTimeOffset now, DateTimeOffset? customExpiresAt = null) =>
        lifetime switch
        {
            Lifetime.Forever => null,
            Lifetime.OneHour => now.AddHours(1),
            Lifetime.OneDay => now.AddDays(1),
            Lifetime.SevenDays => now.AddDays(7),
            Lifetime.Custom when customExpiresAt > now => customExpiresAt,
            Lifetime.Custom => throw new ArgumentException("Custom expiration must be in the future.", nameof(customExpiresAt)),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime))
        };

    public static IQueryable<T> WhereNotExpired<T>(this IQueryable<T> items, DateTimeOffset now)
        where T : IExpiring =>
        items.Where(item => item.ExpiresAt == null || item.ExpiresAt > now);
}
