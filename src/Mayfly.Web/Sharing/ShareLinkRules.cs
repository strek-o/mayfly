using Mayfly.Web.Data;

namespace Mayfly.Web.Sharing;

public static class ShareLinkRules
{
    public static ShareLinkStatus GetStatus(this ShareLink link, DateTimeOffset now) =>
        link.RevokedAt is not null ? ShareLinkStatus.Revoked
        : link.ExpiresAt <= now ? ShareLinkStatus.Expired
        : link.ViewCount >= link.MaxViews ? ShareLinkStatus.UsedUp
        : ShareLinkStatus.Active;

    public static IQueryable<ShareLink> WhereActive(this IQueryable<ShareLink> links, DateTimeOffset now) =>
        links.Where(link =>
            link.RevokedAt == null
            && (link.ExpiresAt == null || link.ExpiresAt > now)
            && (link.MaxViews == null || link.ViewCount < link.MaxViews));
}
