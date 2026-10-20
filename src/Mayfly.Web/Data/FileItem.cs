namespace Mayfly.Web.Data;

public class FileItem
{
    public Guid Id { get; set; }

    public required string OwnerId { get; set; }

    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public long Size { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UploadedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public string BlobName => Id.ToString("N");
}
