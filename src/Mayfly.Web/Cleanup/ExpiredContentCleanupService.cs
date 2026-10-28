using Mayfly.Core.Cleanup;
using Mayfly.Core.Data;
using Mayfly.Core.Files;
using Microsoft.EntityFrameworkCore;

namespace Mayfly.Web.Cleanup;

public sealed class ExpiredContentCleanupService(
    IServiceProvider services,
    IDbContextFactory<MayflyDbContext> dbFactory,
    ILogger<ExpiredContentCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                var storage = services.GetRequiredService<FileStorage>();
                await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);
                var result = await ExpiredContentCleanup.RunAsync(db, storage, DateTimeOffset.UtcNow, stoppingToken);
                logger.LogInformation(
                    "Deleted {ExpiredPastes} expired pastes, {ExpiredFiles} expired files and {AbandonedUploads} abandoned uploads",
                    result.ExpiredPastes,
                    result.ExpiredFiles,
                    result.AbandonedUploads);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Expired content cleanup failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
