namespace Mayfly.Core.Data;

public class ShareLink
{
    public Guid Id { get; set; }

    public Guid? PasteId { get; set; }

    public Guid? ThreadId { get; set; }

    public Guid? FileId { get; set; }

    public Guid? FolderId { get; set; }

    public string? DesktopOwnerId { get; set; }

    public required byte[] TokenHash { get; set; }

    public string? ProtectedToken { get; set; }

    public bool CanAppend { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public int? MaxViews { get; set; }

    public int ViewCount { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? PasswordHash { get; set; }
}
