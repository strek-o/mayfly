using Mayfly.Web.Sharing;

namespace Mayfly.Web.Tests;

public class ShareTokenTests
{
    [Fact]
    public void Generate_ReturnsUrlSafeTokenOf32Bytes()
    {
        var token = ShareToken.Generate();

        Assert.Equal(43, token.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", token);
        Assert.True(ShareToken.IsWellFormed(token));
    }

    [Fact]
    public void Generate_ReturnsDifferentTokensEachTime()
    {
        var tokens = Enumerable.Range(0, 1000).Select(_ => ShareToken.Generate()).ToHashSet();

        Assert.Equal(1000, tokens.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void IsWellFormed_RejectsMalformedTokens(string? token)
    {
        Assert.False(ShareToken.IsWellFormed(token));
    }

    [Fact]
    public void Hash_IsDeterministicAnd32Bytes()
    {
        var token = ShareToken.Generate();

        var first = ShareToken.Hash(token);
        var second = ShareToken.Hash(token);

        Assert.Equal(32, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Hash_DiffersForDifferentTokens()
    {
        Assert.NotEqual(ShareToken.Hash(ShareToken.Generate()), ShareToken.Hash(ShareToken.Generate()));
    }
}
