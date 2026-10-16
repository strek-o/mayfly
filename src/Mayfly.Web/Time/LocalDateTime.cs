namespace Mayfly.Web.Time;

public static class LocalDateTime
{
    public static DateTimeOffset ToUtc(DateTime localDateTime, string? timeZoneId)
    {
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);

        if (timeZoneId is null || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var timeZone))
        {
            return new DateTimeOffset(unspecified, TimeSpan.Zero);
        }

        return new DateTimeOffset(unspecified, timeZone.GetUtcOffset(unspecified)).ToUniversalTime();
    }
}
