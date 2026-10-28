using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Mayfly.Core.Time;

namespace Mayfly.Core.Cleanup;

public static class ExpiredContentCleanup
{
    public const int FileBatchSize = 1000;

    public static async Task<CleanupResult> RunAsync(
        MayflyDbContext db,
        FileStorage storage,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var abandonedBefore = now - FileStorage.UploadLifetime;

        var expiredPastes = await db.Pastes.WhereExpired(now).DeleteWithShareLinksAsync(db, cancellationToken);
        var expiredFiles = await db.Files
            .WhereExpired(now)
            .OrderBy(file => file.ExpiresAt)
            .Take(FileBatchSize)
            .DeleteWithBlobsAsync(db, storage, cancellationToken);
        var abandonedUploads = await db.Files
            .Where(file => file.UploadedAt == null && file.CreatedAt <= abandonedBefore)
            .OrderBy(file => file.CreatedAt)
            .Take(FileBatchSize)
            .DeleteWithBlobsAsync(db, storage, cancellationToken);

        return new CleanupResult(expiredPastes, expiredFiles, abandonedUploads);
    }
}

public sealed record CleanupResult(int ExpiredPastes, int ExpiredFiles, int AbandonedUploads);
