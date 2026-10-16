namespace Mayfly.Web.Data;

public class ShareLink
{
    public Guid Id { get; set; }

    public Guid PasteId { get; set; }

    public Guid? ThreadId { get; set; }

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
