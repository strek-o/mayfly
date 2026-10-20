using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Net.Http.Headers;

namespace Mayfly.Web.Files;

public sealed class FileStorage(BlobServiceClient blobService)
{
    public const string ContainerName = "files";

    public static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan DownloadLifetime = TimeSpan.FromMinutes(5);

    private const string DownloadContentType = "application/octet-stream";

    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private readonly BlobContainerClient container = blobService.GetBlobContainerClient(ContainerName);

    public Task<Uri> GetUploadUriAsync(string blobName, CancellationToken cancellationToken) =>
        CreateSasUriAsync(CreateSas(blobName, BlobSasPermissions.Create, UploadLifetime), cancellationToken);

    public Task<Uri> GetDownloadUriAsync(string blobName, string fileName, CancellationToken cancellationToken)
    {
        var disposition = new ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(fileName);

        var sas = CreateSas(blobName, BlobSasPermissions.Read, DownloadLifetime);
        sas.ContentDisposition = disposition.ToString();
        sas.ContentType = DownloadContentType;
        return CreateSasUriAsync(sas, cancellationToken);
    }

    public async Task<long?> GetSizeAsync(string blobName, CancellationToken cancellationToken)
    {
        try
        {
            var properties = await container.GetBlobClient(blobName).GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.ContentLength;
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken) =>
        container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    private BlobSasBuilder CreateSas(string blobName, BlobSasPermissions permissions, TimeSpan lifetime)
    {
        var now = DateTimeOffset.UtcNow;
        return new BlobSasBuilder(permissions, now.Add(lifetime))
        {
            BlobContainerName = container.Name,
            BlobName = blobName,
            Resource = "b",
            StartsOn = now.Subtract(ClockSkew)
        };
    }

    private async Task<Uri> CreateSasUriAsync(BlobSasBuilder sas, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(sas.BlobName);
        if (blob.CanGenerateSasUri)
        {
            return blob.GenerateSasUri(sas);
        }

        var key = await blobService.GetUserDelegationKeyAsync(sas.StartsOn, sas.ExpiresOn, cancellationToken);
        return new BlobUriBuilder(blob.Uri)
        {
            Sas = sas.ToSasQueryParameters(key.Value, blobService.AccountName)
        }.ToUri();
    }
}
