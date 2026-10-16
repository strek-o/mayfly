using Mayfly.Web.Data;
using Mayfly.Web.Sharing;

namespace Mayfly.Web.Tests;

public class ShareLinkPasswordTests
{
    [Fact]
    public void LinkWithoutPassword_AcceptsAnyInput()
    {
        var link = NewLink();

        Assert.True(ShareLinkPassword.Verify(link, null));
        Assert.True(ShareLinkPassword.Verify(link, "anything"));
    }

    [Fact]
    public void Hash_DoesNotContainThePassword()
    {
        var hash = ShareLinkPassword.Hash(NewLink(), "correct horse");

        Assert.DoesNotContain("correct horse", hash);
    }

    [Fact]
    public void Hash_IsSaltedSoSamePasswordGivesDifferentHashes()
    {
        Assert.NotEqual(
            ShareLinkPassword.Hash(NewLink(), "correct horse"),
            ShareLinkPassword.Hash(NewLink(), "correct horse"));
    }

    [Fact]
    public void ProtectedLink_AcceptsOnlyTheCorrectPassword()
    {
        var link = NewLink();
        link.PasswordHash = ShareLinkPassword.Hash(link, "correct horse");

        Assert.True(ShareLinkPassword.Verify(link, "correct horse"));
        Assert.False(ShareLinkPassword.Verify(link, "Correct horse"));
        Assert.False(ShareLinkPassword.Verify(link, ""));
        Assert.False(ShareLinkPassword.Verify(link, null));
    }

    private static ShareLink NewLink() => new() { TokenHash = new byte[32] };
}
