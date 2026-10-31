using System.Security.Claims;
using Mayfly.Core.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Files;

public static class DesktopLayoutEndpoint
{
    public const int MaxCell = 500;

    public static IEndpointRouteBuilder MapDesktopLayoutEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/desktop/layout", SaveAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> SaveAsync(
        [FromForm] LayoutForm form,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        IDbContextFactory<MayflyDbContext> dbFactory,
        CancellationToken cancellationToken)
    {
        if (form.Items.Any(item => item.Column is < 1 or > MaxCell || item.Row is < 1 or > MaxCell))
        {
            return Results.BadRequest();
        }

        var ownerId = userManager.GetUserId(user)
            ?? throw new InvalidOperationException("Signed-in user has no id claim.");
        var ids = form.Items.Select(item => item.Id).ToList();

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var files = await db.Files.Where(file => ids.Contains(file.Id) && file.OwnerId == ownerId).ToListAsync(cancellationToken);
        var folders = await db.Folders.Where(folder => ids.Contains(folder.Id) && folder.OwnerId == ownerId).ToListAsync(cancellationToken);

        foreach (var item in form.Items)
        {
            if (files.Find(file => file.Id == item.Id) is { } file)
            {
                (file.GridColumn, file.GridRow) = (item.Column, item.Row);
            }
            else if (folders.Find(folder => folder.Id == item.Id) is { } folder)
            {
                (folder.GridColumn, folder.GridRow) = (item.Column, item.Row);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    public sealed class LayoutForm
    {
        public List<ItemPosition> Items { get; set; } = [];
    }

    public sealed class ItemPosition
    {
        public Guid Id { get; set; }

        public int Column { get; set; }

        public int Row { get; set; }
    }
}
