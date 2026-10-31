using System.Security.Claims;
using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Mayfly.Core.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Files;

public static class FileDownloadEndpoint
{
    public static IEndpointRouteBuilder MapFileDownloadEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/files/{id:guid}/{mode:regex(^(download|open)$)}", RedirectAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> RedirectAsync(
        Guid id,
        string mode,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        FileStorage storage,
        IDbContextFactory<MayflyDbContext> dbFactory,
        CancellationToken cancellationToken)
    {
        var ownerId = userManager.GetUserId(user);
        var now = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.Files
            .AsNoTracking()
            .WhereNotExpired(now)
            .WhereFolderNotExpired(db.Folders, now)
            .SingleOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId && f.UploadedAt != null, cancellationToken);

        if (file is null)
        {
            return Results.NotFound();
        }

        var url = mode == "open"
            ? await storage.GetOpenUriAsync(file.BlobName, file.FileName, file.ContentType, cancellationToken)
            : await storage.GetDownloadUriAsync(file.BlobName, file.FileName, cancellationToken);
        return Results.Redirect(url.ToString());
    }
}
