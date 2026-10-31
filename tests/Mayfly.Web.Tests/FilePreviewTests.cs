using Mayfly.Core.Files;

namespace Mayfly.Web.Tests;

public class FilePreviewTests
{
    [Theory]
    [InlineData("image/png", "image/png")]
    [InlineData("IMAGE/JPEG", "image/jpeg")]
    [InlineData("application/pdf", "application/pdf")]
    [InlineData("video/mp4", "video/mp4")]
    [InlineData("audio/mpeg", "audio/mpeg")]
    [InlineData("text/plain", "text/plain; charset=utf-8")]
    [InlineData("text/csv", "text/plain; charset=utf-8")]
    [InlineData("text/html", "text/plain; charset=utf-8")]
    [InlineData("application/json", "text/plain; charset=utf-8")]
    public void ContentTypeFor_OpensOnlyAsASafeType(string stored, string served)
    {
        Assert.Equal(served, FilePreview.ContentTypeFor(stored));
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("application/xhtml+xml")]
    [InlineData("video/mp4,text/html")]
    [InlineData("audio/mpeg, text/html")]
    [InlineData("video/x-unknown")]
    [InlineData("application/octet-stream")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("")]
    public void ContentTypeFor_DownloadsEverythingElse(string stored)
    {
        Assert.Null(FilePreview.ContentTypeFor(stored));
        Assert.False(FilePreview.CanOpen(stored));
    }
}
