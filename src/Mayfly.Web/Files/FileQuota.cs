using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Mayfly.Core.Time;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Files;

public static class FileQuota
{
    public const long MaxBytesPerUser = 500L * 1024 * 1024;

    public static Task<long> GetUsedBytesAsync(
        this MayflyDbContext db,
        string ownerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var pendingSince = now - FileStorage.UploadLifetime;
        return db.Files
            .Where(file => file.OwnerId == ownerId)
            .Where(file => file.UploadedAt != null || file.CreatedAt > pendingSince)
            .WhereNotExpired(now)
            .WhereFolderNotExpired(db.Folders, now)
            .SumAsync(file => file.Size, cancellationToken);
    }
}
