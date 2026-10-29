using System.Security.Claims;
using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Mayfly.Core.Time;
using Mayfly.Web.Time;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Files;

public static class FileUploadEndpoints
{
    public const long MaxFileSize = 100L * 1024 * 1024;

    public const int MaxFileNameLength = 255;

    public const int MaxContentTypeLength = 255;

    private const string DefaultContentType = "application/octet-stream";

    public static IEndpointRouteBuilder MapFileUploadEndpoints(this IEndpointRouteBuilder app)
    {
        var uploads = app.MapGroup("/files/uploads").RequireAuthorization();
        uploads.MapPost("", StartAsync);
        uploads.MapPost("{id:guid}/complete", CompleteAsync);
        return app;
    }

    private static async Task<IResult> StartAsync(
        [FromForm] UploadForm form,
        ClaimsPrincipal user,
        UserManager<ApplicationUser> userManager,
        FileStorage storage,
        IDbContextFactory<MayflyDbContext> dbFactory,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var fileName = form.FileName?.Trim() ?? "";
        var contentType = string.IsNullOrWhiteSpace(form.ContentType) ? DefaultContentType : form.ContentType.Trim();
        DateTimeOffset? customExpiresAt = form.CustomExpiresAt is { } custom
            ? LocalDateTime.ToUtc(custom, form.TimeZone)
            : null;

        if (fileName.Length is 0 or > MaxFileNameLength
            || fileName.Any(char.IsControl)
            || contentType.Length > MaxContentTypeLength
            || form.Size is < 0 or > MaxFileSize
            || !Enum.IsDefined(form.Lifetime)
            || (form.Lifetime == Lifetime.Custom && !(customExpiresAt > now)))
        {
            return Results.BadRequest();
        }

        var ownerId = GetOwnerId(user, userManager);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (form.FolderId is { } folderId
            && !await db.Folders.AnyAsync(folder => folder.Id == folderId && folder.OwnerId == ownerId, cancellationToken))
        {
            return Results.BadRequest();
        }

        var usedBytes = await db.Files.GetUsedBytesAsync(ownerId, now, cancellationToken);
        if (usedBytes + form.Size > FileQuota.MaxBytesPerUser)
        {
            return Results.Conflict();
        }

        var file = new FileItem
        {
            OwnerId = ownerId,
            FolderId = form.FolderId,
            FileName = fileName,
            ContentType = contentType,
            Size = form.Size,
            CreatedAt = now,
            ExpiresAt = Expiration.Calculate(form.Lifetime, now, customExpiresAt)
        };

        db.Files.Add(file);
        await db.SaveChangesAsync(cancellationToken);

        var uploadUrl = await storage.GetUploadUriAsync(file.BlobName, cancellationToken);
        return Results.Ok(new UploadStarted(file.Id, uploadUrl));
    }

    private static async Task<IResult> CompleteAsync(
        Guid id,
        HttpContext context,
        IAntiforgery antiforgery,
        UserManager<ApplicationUser> userManager,
        FileStorage storage,
        IDbContextFactory<MayflyDbContext> dbFactory,
        CancellationToken cancellationToken)
    {
        if (!await antiforgery.IsRequestValidAsync(context))
        {
            return Results.BadRequest();
        }

        var ownerId = GetOwnerId(context.User, userManager);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var file = await db.Files.SingleOrDefaultAsync(
            f => f.Id == id && f.OwnerId == ownerId && f.UploadedAt == null,
            cancellationToken);

        if (file is null)
        {
            return Results.NotFound();
        }

        var uploadedSize = await storage.GetSizeAsync(file.BlobName, cancellationToken);
        if (uploadedSize is null)
        {
            return Results.Conflict();
        }

        if (uploadedSize != file.Size)
        {
            await storage.DeleteAsync(file.BlobName, cancellationToken);
            db.Files.Remove(file);
            await db.SaveChangesAsync(cancellationToken);
            return Results.BadRequest();
        }

        file.UploadedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static string GetOwnerId(ClaimsPrincipal user, UserManager<ApplicationUser> userManager) =>
        userManager.GetUserId(user) ?? throw new InvalidOperationException("Signed-in user has no id claim.");

    public sealed class UploadForm
    {
        public string? FileName { get; set; }

        public string? ContentType { get; set; }

        public long Size { get; set; }

        public Lifetime Lifetime { get; set; }

        public DateTime? CustomExpiresAt { get; set; }

        public string? TimeZone { get; set; }

        public Guid? FolderId { get; set; }
    }

    public sealed record UploadStarted(Guid Id, Uri UploadUrl);
}
