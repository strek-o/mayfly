using Mayfly.Core.Data;
using Mayfly.Core.Time;

namespace Mayfly.Web.Sharing;

public static class ShareLinkRules
{
    public static ShareLinkStatus GetStatus(this ShareLink link, DateTimeOffset now) =>
        link.RevokedAt is not null ? ShareLinkStatus.Revoked
        : link.ExpiresAt <= now ? ShareLinkStatus.Expired
        : link.ViewCount >= link.MaxViews ? ShareLinkStatus.UsedUp
        : ShareLinkStatus.Active;

    public static IQueryable<ShareLink> WhereOpen(this IQueryable<ShareLink> links, DateTimeOffset now) =>
        links.Where(link => link.RevokedAt == null && (link.ExpiresAt == null || link.ExpiresAt > now));

    public static IQueryable<ShareLink> WhereActive(this IQueryable<ShareLink> links, DateTimeOffset now) =>
        links.WhereOpen(now).Where(link => link.MaxViews == null || link.ViewCount < link.MaxViews);

    public static IQueryable<ShareLink> WhereTargetAvailable(this IQueryable<ShareLink> links, MayflyDbContext db, DateTimeOffset now)
    {
        var livePastes = db.Pastes.WhereNotExpired(now);
        var liveFiles = db.Files.Where(file => file.UploadedAt != null).WhereNotExpired(now).WhereFolderNotExpired(db.Folders, now);
        return links.Where(link =>
            livePastes.Any(paste => paste.Id == link.PasteId)
            || liveFiles.Any(file => file.Id == link.FileId)
            || db.Folders.WhereNotExpired(now).Any(folder => folder.Id == link.FolderId)
            || link.DesktopOwnerId != null);
    }
}
