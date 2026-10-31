using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Mayfly.Core.Time;
using Microsoft.EntityFrameworkCore;

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

        var expiredFolders = await DeleteExpiredFoldersAsync(db, storage, now, cancellationToken);

        return new CleanupResult(expiredPastes, expiredFiles, abandonedUploads, expiredFolders);
    }

    private static async Task<int> DeleteExpiredFoldersAsync(
        MayflyDbContext db,
        FileStorage storage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var owners = await db.Folders.WhereExpired(now).Select(folder => folder.OwnerId).Distinct().ToListAsync(cancellationToken);
        var folders = await db.Folders.AsNoTracking().Where(folder => owners.Contains(folder.OwnerId)).ToListAsync(cancellationToken);
        var ids = folders.Expired(now).Select(folder => folder.Id).ToList();

        await db.Files
            .Where(file => file.FolderId != null && ids.Contains(file.FolderId.Value))
            .DeleteWithBlobsAsync(db, storage, cancellationToken);
        await db.ShareLinks.Where(link => link.FolderId != null && ids.Contains(link.FolderId.Value)).ExecuteDeleteAsync(cancellationToken);
        return await db.Folders.Where(folder => ids.Contains(folder.Id)).ExecuteDeleteAsync(cancellationToken);
    }
}

public sealed record CleanupResult(int ExpiredPastes, int ExpiredFiles, int AbandonedUploads, int ExpiredFolders);
