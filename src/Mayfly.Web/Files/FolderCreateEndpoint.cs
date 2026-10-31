using System.Security.Claims;
using Mayfly.Core.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Files;

public static class FolderCreateEndpoint
{
    public const int MaxNameLength = 100;

    public static IEndpointRouteBuilder MapFolderCreateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/folders", CreateAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> CreateAsync(
        [FromForm(Name = "Name")] string? name,
        [FromForm(Name = "ParentId")] Guid? parentId,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<MayflyDbContext> dbFactory,
        CancellationToken cancellationToken)
    {
        name = name?.Trim() ?? "";
        if (name.Length is 0 or > MaxNameLength || name.Any(char.IsControl))
        {
            return Results.BadRequest();
        }

        var ownerId = userManager.GetUserId(user)
            ?? throw new InvalidOperationException("Signed-in user has no id claim.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (parentId is { } parent
            && !await db.Folders.AnyAsync(folder => folder.Id == parent && folder.OwnerId == ownerId, cancellationToken))
        {
            return Results.BadRequest();
        }

        var folder = new Folder { OwnerId = ownerId, ParentId = parentId, Name = name, CreatedAt = DateTimeOffset.UtcNow };
        db.Folders.Add(folder);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new FolderCreated(folder.Id));
    }

    public sealed record FolderCreated(Guid Id);
}
