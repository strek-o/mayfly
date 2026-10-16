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
        HttpContext context,
        ShareSession session,
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
            .WhereOpen(now)
            .Where(l => livePastes.Any(paste => paste.Id == l.PasteId))
            .SingleOrDefaultAsync();

        if (link is null)
        {
            return Results.NotFound();
        }

        if (!session.IsActive(context, link.Id))
        {
            return Results.LocalRedirect($"/s/{token}?entry=session-expired");
        }

        var threadInScope = await db.Threads.AnyAsync(thread =>
            thread.Id == form.ThreadId
            && thread.PasteId == link.PasteId
            && (link.ThreadId == null || thread.Id == link.ThreadId));

        if (!threadInScope)
        {
            return Results.NotFound();
        }

        var entry = new ThreadEntry
        {
            ThreadId = form.ThreadId,
            Content = form.Content,
            AuthorName = authorName,
            CreatedAt = now
        };
        db.Entries.Add(entry);
        await db.SaveChangesAsync();

        session.Start(context, link.Id, token);
        return Results.LocalRedirect($"/s/{token}#entry-{entry.Id}");
    }

    public sealed class GuestEntryForm
    {
        public Guid ThreadId { get; set; }

        public string? Content { get; set; }

        public string? AuthorName { get; set; }
    }
}
