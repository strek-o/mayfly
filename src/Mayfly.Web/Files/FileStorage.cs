using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

namespace Mayfly.Web.Files;

public sealed class FileStorage(BlobServiceClient blobService)
{
    public const string ContainerName = "files";

    public static readonly TimeSpan UploadLifetime = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    private readonly BlobContainerClient container = blobService.GetBlobContainerClient(ContainerName);

    public Task<Uri> GetUploadUriAsync(string blobName, CancellationToken cancellationToken) =>
        CreateSasUriAsync(blobName, BlobSasPermissions.Create, UploadLifetime, cancellationToken);

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

    private async Task<Uri> CreateSasUriAsync(string blobName, BlobSasPermissions permissions, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(blobName);
        var now = DateTimeOffset.UtcNow;
        var sas = new BlobSasBuilder(permissions, now.Add(lifetime))
        {
            BlobContainerName = container.Name,
            BlobName = blobName,
            Resource = "b",
            StartsOn = now.Subtract(ClockSkew)
        };

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
