using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Mayfly.Core.Time;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Sharing;

public static class SharedFileDownloadEndpoint
{
    public static IEndpointRouteBuilder MapSharedFileDownloadEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/s/{token}/download", DownloadAsync).RequireRateLimiting(ShareLinkRateLimit.PolicyName);
        return app;
    }

    private static async Task<IResult> DownloadAsync(
        string token,
        HttpContext context,
        IAntiforgery antiforgery,
        ShareSession session,
        FileStorage storage,
        IDbContextFactory<MayflyDbContext> dbFactory,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest();
        }

        if (!ShareToken.IsWellFormed(token))
        {
            return Results.NotFound();
        }

        var tokenHash = ShareToken.Hash(token);
        var now = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var link = await db.ShareLinks
            .AsNoTracking()
            .Where(l => l.TokenHash == tokenHash && (l.FileId != null || l.FolderId != null))
            .WhereOpen(now)
            .SingleOrDefaultAsync(cancellationToken);

        var form = await context.Request.ReadFormAsync(cancellationToken);
        Guid? requestedFileId = Guid.TryParse(form["FileId"], out var parsed) ? parsed : null;

        var folderIds = link?.FolderId is { } sharedFolderId
            ? (await db.Folders.LoadTreeAsync(sharedFolderId, cancellationToken)).Select(folder => folder.Id).ToList()
            : [];
        var liveFiles = db.Files.AsNoTracking().Where(f => f.UploadedAt != null).WhereNotExpired(now);
        var file = link switch
        {
            null => null,
            { FileId: { } fileId } => await liveFiles.SingleOrDefaultAsync(f => f.Id == fileId, cancellationToken),
            _ => await liveFiles.SingleOrDefaultAsync(
                f => f.Id == requestedFileId && f.FolderId != null && folderIds.Contains(f.FolderId.Value),
                cancellationToken)
        };

        if (link is null || file is null)
        {
            return Results.NotFound();
        }

        if (!session.IsActive(context, link.Id))
        {
            return Results.LocalRedirect($"/s/{token}?session=expired");
        }

        session.Start(context, link.Id, token);
        var downloadUrl = await storage.GetDownloadUriAsync(file.BlobName, file.FileName, cancellationToken);
        return Results.Redirect(downloadUrl.ToString());
    }
}
