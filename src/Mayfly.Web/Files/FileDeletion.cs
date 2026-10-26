using Mayfly.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Files;

public static class FileDeletion
{
    public static async Task DeleteWithBlobsAsync(
        this IQueryable<FileItem> files,
        MayflyDbContext db,
        FileStorage storage,
        CancellationToken cancellationToken = default)
    {
        var selected = await files.AsNoTracking().ToListAsync(cancellationToken);
        foreach (var file in selected)
        {
            await storage.DeleteAsync(file.BlobName, cancellationToken);
        }

        var ids = selected.Select(file => file.Id).ToList();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.ShareLinks
                .Where(link => link.FileId != null && ids.Contains(link.FileId.Value))
                .ExecuteDeleteAsync(cancellationToken);
            await db.Files.Where(file => ids.Contains(file.Id)).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }
}
