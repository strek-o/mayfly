using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Mayfly.Core.Files;

namespace Mayfly.Web.Files;

public sealed class LocalStorageSetup(BlobServiceClient blobService, ILogger<LocalStorageSetup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await blobService.GetBlobContainerClient(FileStorage.ContainerName).CreateIfNotExistsAsync(cancellationToken: stoppingToken);
            BlobServiceProperties properties = await blobService.GetPropertiesAsync(stoppingToken);
            properties.Cors =
            [
                new BlobCorsRule
                {
                    AllowedOrigins = "*",
                    AllowedMethods = "PUT",
                    AllowedHeaders = "content-type,x-ms-*",
                    ExposedHeaders = "etag,x-ms-request-id",
                    MaxAgeInSeconds = 3600
                }
            ];
            await blobService.SetPropertiesAsync(properties, stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Local blob storage is not available. Start it with docker compose up -d.");
        }
    }
}
