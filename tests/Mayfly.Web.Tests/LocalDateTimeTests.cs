using Mayfly.Web.Time;

namespace Mayfly.Web.Tests;

public class LocalDateTimeTests
{
    [Fact]
    public void SummerTimeInWarsaw_IsTwoHoursAheadOfUtc()
    {
        var utc = LocalDateTime.ToUtc(new DateTime(2026, 10, 16, 14, 0, 0), "Europe/Warsaw");

        Assert.Equal(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void WinterTimeInWarsaw_IsOneHourAheadOfUtc()
    {
        var utc = LocalDateTime.ToUtc(new DateTime(2026, 12, 1, 14, 0, 0), "Europe/Warsaw");

        Assert.Equal(new DateTimeOffset(2026, 12, 1, 13, 0, 0, TimeSpan.Zero), utc);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public void MissingOrUnknownZone_FallsBackToUtc(string? timeZoneId)
    {
        var utc = LocalDateTime.ToUtc(new DateTime(2026, 10, 16, 14, 0, 0), timeZoneId);

        Assert.Equal(new DateTimeOffset(2026, 10, 16, 14, 0, 0, TimeSpan.Zero), utc);
    }
}
