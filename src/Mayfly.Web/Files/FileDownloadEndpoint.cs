using System.Security.Claims;
using Mayfly.Web.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Files;

public static class FileDownloadEndpoint
{
    public static IEndpointRouteBuilder MapFileDownloadEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/files/{id:guid}/download", DownloadAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> DownloadAsync(
        Guid id,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        FileStorage storage,
        IDbContextFactory<MayflyDbContext> dbFactory,
        CancellationToken cancellationToken)
    {
        var ownerId = userManager.GetUserId(user);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.Files
            .AsNoTracking()
            .SingleOrDefaultAsync(f => f.Id == id && f.OwnerId == ownerId && f.UploadedAt != null, cancellationToken);

        if (file is null)
        {
            return Results.NotFound();
        }

        var downloadUrl = await storage.GetDownloadUriAsync(file.BlobName, file.FileName, cancellationToken);
        return Results.Redirect(downloadUrl.ToString());
    }
}
