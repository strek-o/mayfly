using Mayfly.Core.Cleanup;
using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Mayfly.Functions;

public sealed class CleanupFunction(
    IDbContextFactory<MayflyDbContext> dbFactory,
    FileStorage storage,
    ILogger<CleanupFunction> logger)
{
    [Function("Cleanup")]
    public async Task RunAsync([TimerTrigger("0 */15 * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var result = await ExpiredContentCleanup.RunAsync(db, storage, DateTimeOffset.UtcNow, cancellationToken);

        logger.LogInformation(
            "Deleted {ExpiredPastes} expired pastes, {ExpiredFiles} expired files and {AbandonedUploads} abandoned uploads",
            result.ExpiredPastes,
            result.ExpiredFiles,
            result.AbandonedUploads);
    }
}
