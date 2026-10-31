namespace Mayfly.Core.Files;

public static class FilePreview
{
    private const string PlainText = "text/plain; charset=utf-8";

    public static bool CanOpen(string contentType) => ContentTypeFor(contentType) is not null;

    public static string? ContentTypeFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        var type when type is "image/png" or "image/jpeg" or "image/gif" or "image/webp" or "image/avif" or "application/pdf"
            or "audio/mpeg" or "audio/mp4" or "audio/ogg" or "audio/wav" or "audio/flac" or "audio/webm"
            or "video/mp4" or "video/webm" or "video/ogg" => type,
        var type when type.StartsWith("text/", StringComparison.Ordinal) || type is "application/json" or "application/xml" => PlainText,
        _ => null
    };
}
