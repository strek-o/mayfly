using System.Text.RegularExpressions;
using Mayfly.Web.Pastes;

namespace Mayfly.Web.Tests;

public class EntryMarkdownTests
{
    [Fact]
    public void ToHtml_RendersFormatting()
    {
        var html = EntryMarkdown.ToHtml("**bold** _italic_ ~~gone~~ ++underlined++ `code`").Value;

        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("<em>italic</em>", html);
        Assert.Contains("<del>gone</del>", html);
        Assert.Contains("<ins>underlined</ins>", html);
        Assert.Contains("<code>code</code>", html);
    }

    [Fact]
    public void ToHtml_KeepsLineBreaks()
    {
        Assert.Contains("<br />", EntryMarkdown.ToHtml("first\nsecond").Value);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("text <b onclick=alert(1)>bold</b>")]
    public void ToHtml_EscapesRawHtml(string content)
    {
        var html = EntryMarkdown.ToHtml(content).Value;

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<b ", html);
        Assert.Contains("&lt;", html);
    }

    [Theory]
    [InlineData("[click](javascript:alert(1))")]
    [InlineData("[click](JavaScript:alert(1))")]
    [InlineData("[click](java&#x73;cript:alert(1))")]
    [InlineData("[click](data:text/html;base64,PHNjcmlwdD4=)")]
    [InlineData("<javascript:alert(1)>")]
    [InlineData("[click](vbscript:msgbox(1))")]
    [InlineData("ftp://example.com/file")]
    public void ToHtml_NeutralizesUnsafeLinks(string content)
    {
        var hrefs = Regex.Matches(EntryMarkdown.ToHtml(content).Value, "href=\"([^\"]*)\"")
            .Select(match => match.Groups[1].Value)
            .ToList();

        Assert.NotEmpty(hrefs);
        Assert.All(hrefs, href => Assert.Equal("#", href));
    }

    [Theory]
    [InlineData("[docs](https://learn.microsoft.com/azure)", "https://learn.microsoft.com/azure")]
    [InlineData("<https://example.com/a>", "https://example.com/a")]
    [InlineData("[mail](mailto:owner@example.test)", "mailto:owner@example.test")]
    [InlineData("see https://example.com/b for details", "https://example.com/b")]
    [InlineData("www.example.com", "http://www.example.com")]
    public void ToHtml_KeepsSafeLinksWithoutReferrer(string content, string url)
    {
        var html = EntryMarkdown.ToHtml(content).Value;

        Assert.Contains($"href=\"{url}\"", html);
        Assert.Contains("rel=\"nofollow noopener noreferrer\"", html);
    }

    [Fact]
    public void ToHtml_TurnsImagesIntoLinks()
    {
        var html = EntryMarkdown.ToHtml("![tracking pixel](https://example.com/pixel.png)").Value;

        Assert.DoesNotContain("<img", html);
        Assert.Contains("href=\"https://example.com/pixel.png\"", html);
        Assert.Contains("tracking pixel", html);
    }
}
