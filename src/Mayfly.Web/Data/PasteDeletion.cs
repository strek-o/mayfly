using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Data;

public static class PasteDeletion
{
    public static Task DeleteWithShareLinksAsync(this IQueryable<Paste> pastes, MayflyDbContext db) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.ShareLinks.Where(link => pastes.Any(paste => paste.Id == link.PasteId)).ExecuteDeleteAsync();
            await pastes.ExecuteDeleteAsync();
            await transaction.CommitAsync();
        });
}
