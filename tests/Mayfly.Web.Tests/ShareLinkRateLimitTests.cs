using System.Net;
using Mayfly.Web.Sharing;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mayfly.Web.Tests;

public class ShareLinkRateLimitTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("GET", "/s/not-a-real-token", "203.0.113.1")]
    [InlineData("POST", "/s/not-a-real-token", "203.0.113.2")]
    [InlineData("POST", "/s/not-a-real-token/entries", "203.0.113.3")]
    [InlineData("POST", "/s/not-a-real-token/download", "203.0.113.4")]
    public async Task ShareLinks_AreLimitedPerClientAddress(string method, string url, string clientAddress)
    {
        var client = factory.CreateClient();

        var allowed = new List<HttpStatusCode>();
        for (var i = 0; i < ShareLinkRateLimit.PermitLimit; i++)
        {
            allowed.Add((await SendAsync(client, method, url, clientAddress)).StatusCode);
        }

        var rejected = await SendAsync(client, method, url, clientAddress);
        var otherClient = await SendAsync(client, method, url, "198.51.100.1");

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, allowed);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Contains("Too many requests", await rejected.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, otherClient.StatusCode);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url, string clientAddress)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Add("X-Forwarded-For", clientAddress);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }
}
