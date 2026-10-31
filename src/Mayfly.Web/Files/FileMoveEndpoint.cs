using System.Security.Claims;
using Mayfly.Core.Data;
using Mayfly.Core.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Files;

public static class FileMoveEndpoint
{
    public static IEndpointRouteBuilder MapFileMoveEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/files/{id:guid}/move", MoveAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> MoveAsync(
        Guid id,
        [FromForm(Name = "FolderId")] Guid? folderId,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<MayflyDbContext> dbFactory,
        CancellationToken cancellationToken)
    {
        var ownerId = userManager.GetUserId(user);
        var now = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (folderId is { } target
            && !await db.Folders.WhereNotExpired(now).AnyAsync(folder => folder.Id == target && folder.OwnerId == ownerId, cancellationToken))
        {
            return Results.BadRequest();
        }

        var moved = await db.Files
            .Where(file => file.Id == id && file.OwnerId == ownerId && file.UploadedAt != null)
            .WhereNotExpired(now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(file => file.FolderId, folderId)
                    .SetProperty(file => file.GridColumn, (int?)null)
                    .SetProperty(file => file.GridRow, (int?)null),
                cancellationToken);

        return moved == 0 ? Results.NotFound() : Results.NoContent();
    }
}
