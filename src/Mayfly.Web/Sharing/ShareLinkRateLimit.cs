using System.Threading.RateLimiting;

namespace Mayfly.Web.Sharing;

public static class ShareLinkRateLimit
{
    public const string PolicyName = "share-links";

    public const int PermitLimit = 30;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddShareLinkRateLimit(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) =>
                new ValueTask(context.HttpContext.Response.WriteAsync("Too many requests. Try again in a minute.", cancellationToken));
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                _ => new FixedWindowRateLimiterOptions { PermitLimit = PermitLimit, Window = Window }));
        });
}
