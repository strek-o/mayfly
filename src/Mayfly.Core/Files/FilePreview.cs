namespace Mayfly.Core.Files;

public static class FilePreview
{
    private const string PlainText = "text/plain; charset=utf-8";

    public static bool CanOpen(string contentType) => ContentTypeFor(contentType) is not null;

    public static string? ContentTypeFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" or "image/jpeg" or "image/gif" or "image/webp" or "image/avif" or "application/pdf" => contentType.ToLowerInvariant(),
        var type when type.StartsWith("audio/", StringComparison.Ordinal) || type.StartsWith("video/", StringComparison.Ordinal) => type,
        var type when type.StartsWith("text/", StringComparison.Ordinal) || type is "application/json" or "application/xml" => PlainText,
        _ => null
    };
}
