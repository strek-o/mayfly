using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;

namespace Mayfly.Web.Pastes;

public static class EntryMarkdown
{
    private static readonly string[] SafeSchemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeMailto];

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough | EmphasisExtraOptions.Inserted)
        .UseSoftlineBreakAsHardlineBreak()
        .UseReferralLinks("nofollow", "noopener", "noreferrer")
        .Build();

    public static MarkupString ToHtml(string content)
    {
        var document = Markdown.Parse(content, Pipeline);

        foreach (var link in document.Descendants<LinkInline>())
        {
            link.IsImage = false;
            if (!IsSafe(link.Url))
            {
                link.Url = "#";
            }
        }

        foreach (var link in document.Descendants<AutolinkInline>())
        {
            if (!IsSafe(link.Url))
            {
                link.Url = "#";
            }
        }

        return new MarkupString(document.ToHtml(Pipeline));
    }

    private static bool IsSafe(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && SafeSchemes.Contains(uri.Scheme);
}
