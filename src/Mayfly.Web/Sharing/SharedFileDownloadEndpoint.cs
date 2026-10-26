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
        app.MapPost("/s/{token}/download", DownloadAsync);
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
            .Where(l => l.TokenHash == tokenHash && l.FileId != null)
            .WhereOpen(now)
            .SingleOrDefaultAsync(cancellationToken);

        var file = link is null
            ? null
            : await db.Files
                .AsNoTracking()
                .Where(f => f.Id == link.FileId && f.UploadedAt != null)
                .WhereNotExpired(now)
                .SingleOrDefaultAsync(cancellationToken);

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
