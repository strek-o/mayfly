using System.Security.Claims;
using Mayfly.Core.Data;
using Mayfly.Core.Time;
using Mayfly.Web.Time;
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
        [FromForm] FolderForm form,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<MayflyDbContext> dbFactory,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var name = form.Name?.Trim() ?? "";
        DateTimeOffset? customExpiresAt = form.CustomExpiresAt is { } custom
            ? LocalDateTime.ToUtc(custom, form.TimeZone)
            : null;

        if (name.Length is 0 or > MaxNameLength
            || name.Any(char.IsControl)
            || !Enum.IsDefined(form.Lifetime)
            || (form.Lifetime == Lifetime.Custom && !(customExpiresAt > now)))
        {
            return Results.BadRequest();
        }

        var ownerId = userManager.GetUserId(user)
            ?? throw new InvalidOperationException("Signed-in user has no id claim.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        DateTimeOffset? parentExpiresAt = null;
        if (form.ParentId is { } parentId)
        {
            var parent = await db.Folders
                .WhereNotExpired(now)
                .Where(folder => folder.Id == parentId && folder.OwnerId == ownerId)
                .Select(folder => new { folder.ExpiresAt })
                .SingleOrDefaultAsync(cancellationToken);
            if (parent is null)
            {
                return Results.BadRequest();
            }

            parentExpiresAt = parent.ExpiresAt;
        }

        var folder = new Folder
        {
            OwnerId = ownerId,
            ParentId = form.ParentId,
            Name = name,
            CreatedAt = now,
            ExpiresAt = Expiration.Earliest(Expiration.Calculate(form.Lifetime, now, customExpiresAt), parentExpiresAt)
        };
        db.Folders.Add(folder);
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new FolderCreated(folder.Id));
    }

    public sealed class FolderForm
    {
        public string? Name { get; set; }

        public Guid? ParentId { get; set; }

        public Lifetime Lifetime { get; set; }

        public DateTime? CustomExpiresAt { get; set; }

        public string? TimeZone { get; set; }
    }

    public sealed record FolderCreated(Guid Id);
}
