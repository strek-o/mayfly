using Mayfly.Web.Data;
using Mayfly.Web.Pastes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Sharing;

public static class GuestEntryEndpoint
{
    public const int MaxContentLength = 100_000;

    public const int MaxAuthorNameLength = 50;

    public static IEndpointRouteBuilder MapGuestEntryEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/s/{token}/entries", AddEntryAsync);
        return app;
    }

    private static async Task<IResult> AddEntryAsync(
        string token,
        [FromForm] GuestEntryForm form,
        IDbContextFactory<MayflyDbContext> dbFactory)
    {
        if (!ShareToken.IsWellFormed(token))
        {
            return Results.NotFound();
        }

        var authorName = string.IsNullOrWhiteSpace(form.AuthorName) ? "Guest" : form.AuthorName.Trim();
        if (string.IsNullOrWhiteSpace(form.Content)
            || form.Content.Length > MaxContentLength
            || authorName.Length > MaxAuthorNameLength)
        {
            return Results.BadRequest();
        }

        var tokenHash = ShareToken.Hash(token);
        var now = DateTimeOffset.UtcNow;

        await using var db = await dbFactory.CreateDbContextAsync();
        var livePastes = db.Pastes.WhereNotExpired(now);
        var link = await db.ShareLinks
            .AsNoTracking()
            .Where(l => l.TokenHash == tokenHash && l.CanAppend)
            .WhereActive(now)
            .Where(l => livePastes.Any(paste => paste.Id == l.PasteId))
            .SingleOrDefaultAsync();

        if (link is null)
        {
            return Results.NotFound();
        }

        if (!ShareLinkPassword.Verify(link, form.Password))
        {
            return Results.LocalRedirect($"/s/{token}?entry=wrong-password");
        }

        var threadInScope = await db.Threads.AnyAsync(thread =>
            thread.Id == form.ThreadId
            && thread.PasteId == link.PasteId
            && (link.ThreadId == null || thread.Id == link.ThreadId));

        if (!threadInScope)
        {
            return Results.NotFound();
        }

        db.Entries.Add(new ThreadEntry
        {
            ThreadId = form.ThreadId,
            Content = form.Content,
            AuthorName = authorName,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        return Results.LocalRedirect($"/s/{token}?entry=added");
    }

    public sealed class GuestEntryForm
    {
        public Guid ThreadId { get; set; }

        public string? Content { get; set; }

        public string? AuthorName { get; set; }

        public string? Password { get; set; }
    }
}
