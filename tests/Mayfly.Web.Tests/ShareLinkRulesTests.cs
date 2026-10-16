using Mayfly.Web.Data;
using Mayfly.Web.Sharing;

namespace Mayfly.Web.Tests;

public class ShareLinkRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string, ShareLink, ShareLinkStatus> Cases => new()
    {
        { "no limits", NewLink(), ShareLinkStatus.Active },
        { "expires later", NewLink(expiresAt: Now.AddMinutes(1)), ShareLinkStatus.Active },
        { "views left", NewLink(maxViews: 3, viewCount: 2), ShareLinkStatus.Active },
        { "expired exactly now", NewLink(expiresAt: Now), ShareLinkStatus.Expired },
        { "expired earlier", NewLink(expiresAt: Now.AddMinutes(-1)), ShareLinkStatus.Expired },
        { "burned after read", NewLink(maxViews: 1, viewCount: 1), ShareLinkStatus.UsedUp },
        { "revoked", NewLink(revokedAt: Now.AddDays(-1)), ShareLinkStatus.Revoked },
        { "revoked wins over expired", NewLink(expiresAt: Now.AddMinutes(-1), revokedAt: Now.AddDays(-1)), ShareLinkStatus.Revoked },
        { "expired wins over used up", NewLink(expiresAt: Now.AddMinutes(-1), maxViews: 1, viewCount: 1), ShareLinkStatus.Expired }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void GetStatus_ReturnsExpectedStatus(string scenario, ShareLink link, ShareLinkStatus expected)
    {
        Assert.True(expected == link.GetStatus(Now), scenario);
    }

    [Fact]
    public void WhereActive_MatchesGetStatus()
    {
        var links = Cases.Select(row => row.Data.Item2).ToList();

        var active = links.AsQueryable().WhereActive(Now).ToList();

        Assert.Equal(links.Where(link => link.GetStatus(Now) == ShareLinkStatus.Active), active);
        Assert.Equal(3, active.Count);
    }

    [Fact]
    public void WhereOpen_KeepsActiveAndUsedUpLinks()
    {
        var links = Cases.Select(row => row.Data.Item2).ToList();

        var open = links.AsQueryable().WhereOpen(Now).ToList();

        Assert.Equal(
            links.Where(link => link.GetStatus(Now) is ShareLinkStatus.Active or ShareLinkStatus.UsedUp),
            open);
        Assert.Equal(4, open.Count);
    }

    private static ShareLink NewLink(
        DateTimeOffset? expiresAt = null,
        int? maxViews = null,
        int viewCount = 0,
        DateTimeOffset? revokedAt = null) =>
        new()
        {
            TokenHash = new byte[32],
            ExpiresAt = expiresAt,
            MaxViews = maxViews,
            ViewCount = viewCount,
            RevokedAt = revokedAt
        };
}
