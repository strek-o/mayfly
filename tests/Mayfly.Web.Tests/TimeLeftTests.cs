using Mayfly.Web.Time;

namespace Mayfly.Web.Tests;

public class TimeLeftTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 14, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(-30, "1 min")]
    [InlineData(30, "1 min")]
    [InlineData(12 * 60, "12 min")]
    [InlineData(59 * 60, "59 min")]
    [InlineData(59 * 60 + 30, "1 h")]
    [InlineData(19 * 3600 + 40 * 60, "20 h")]
    [InlineData(23 * 3600 + 29 * 60, "23 h")]
    [InlineData(23 * 3600 + 59 * 60, "1 d")]
    [InlineData(3 * 86400 + 4 * 3600, "3 d")]
    [InlineData(7 * 86400 - 60, "7 d")]
    [InlineData(32 * 86400, "32 d")]
    public void Format_RoundsToTheNearestLargestUnit(int secondsLeft, string expected)
    {
        Assert.Equal(expected, TimeLeft.Format(Now.AddSeconds(secondsLeft), Now));
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(59 * 60, true)]
    [InlineData(59 * 60 + 30, false)]
    [InlineData(2 * 3600, false)]
    public void IsSoon_MatchesTheMinuteLabels(int secondsLeft, bool expected)
    {
        Assert.Equal(expected, TimeLeft.IsSoon(Now.AddSeconds(secondsLeft), Now));
    }

    [Fact]
    public void IsSoon_WithoutExpiry_IsFalse()
    {
        Assert.False(TimeLeft.IsSoon(null, Now));
    }
}
