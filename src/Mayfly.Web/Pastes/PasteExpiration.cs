using Mayfly.Web.Data;

namespace Mayfly.Web.Pastes;

public static class PasteExpiration
{
    public static DateTimeOffset? Calculate(PasteLifetime lifetime, DateTimeOffset now, DateTimeOffset? customExpiresAt = null) =>
        lifetime switch
        {
            PasteLifetime.Forever => null,
            PasteLifetime.OneHour => now.AddHours(1),
            PasteLifetime.OneDay => now.AddDays(1),
            PasteLifetime.SevenDays => now.AddDays(7),
            PasteLifetime.Custom when customExpiresAt > now => customExpiresAt,
            PasteLifetime.Custom => throw new ArgumentException("Custom expiration must be in the future.", nameof(customExpiresAt)),
            _ => throw new ArgumentOutOfRangeException(nameof(lifetime))
        };

    public static IQueryable<Paste> WhereNotExpired(this IQueryable<Paste> pastes, DateTimeOffset now) =>
        pastes.Where(paste => paste.ExpiresAt == null || paste.ExpiresAt > now);
}
