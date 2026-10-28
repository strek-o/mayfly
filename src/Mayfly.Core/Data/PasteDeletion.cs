using Microsoft.EntityFrameworkCore;

namespace Mayfly.Core.Data;

public static class PasteDeletion
{
    public static Task<int> DeleteWithShareLinksAsync(
        this IQueryable<Paste> pastes,
        MayflyDbContext db,
        CancellationToken cancellationToken = default) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.ShareLinks
                .Where(link => pastes.Any(paste => paste.Id == link.PasteId))
                .ExecuteDeleteAsync(cancellationToken);
            var deleted = await pastes.ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return deleted;
        });
}
