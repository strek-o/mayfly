using Mayfly.Web.Data;
using Mayfly.Web.Time;

namespace Mayfly.Web.Tests;

public class ExpirationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Forever_HasNoExpiration()
    {
        Assert.Null(Expiration.Calculate(Lifetime.Forever, Now));
    }

    [Theory]
    [InlineData(Lifetime.OneHour, 1)]
    [InlineData(Lifetime.OneDay, 24)]
    [InlineData(Lifetime.SevenDays, 168)]
    public void Preset_ExpiresAfterItsDuration(Lifetime lifetime, int hours)
    {
        Assert.Equal(Now.AddHours(hours), Expiration.Calculate(lifetime, Now));
    }

    [Fact]
    public void Custom_InTheFuture_IsUsedAsIs()
    {
        var custom = Now.AddDays(30);

        Assert.Equal(custom, Expiration.Calculate(Lifetime.Custom, Now, custom));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Custom_NowOrInThePast_IsRejected(int minutesFromNow)
    {
        Assert.Throws<ArgumentException>(() =>
            Expiration.Calculate(Lifetime.Custom, Now, Now.AddMinutes(minutesFromNow)));
    }

    [Fact]
    public void Custom_WithoutDate_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Expiration.Calculate(Lifetime.Custom, Now));
    }

    [Fact]
    public void WhereNotExpired_KeepsPermanentAndFuturePastes()
    {
        var pastes = new[]
        {
            NewPaste("permanent", null),
            NewPaste("future", Now.AddMinutes(1)),
            NewPaste("exactly-now", Now),
            NewPaste("past", Now.AddMinutes(-1))
        };

        var visible = pastes.AsQueryable().WhereNotExpired(Now).Select(paste => paste.Title);

        Assert.Equal(["permanent", "future"], visible);
    }

    private static Paste NewPaste(string title, DateTimeOffset? expiresAt) =>
        new() { OwnerId = "owner", Title = title, ExpiresAt = expiresAt };
}
