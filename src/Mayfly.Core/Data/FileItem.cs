namespace Mayfly.Core.Data;

public class FileItem : IExpiring
{
    public Guid Id { get; set; }

    public required string OwnerId { get; set; }

    public Guid? FolderId { get; set; }

    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public long Size { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UploadedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public int? GridColumn { get; set; }

    public int? GridRow { get; set; }

    public string BlobName => Id.ToString("N");
}
