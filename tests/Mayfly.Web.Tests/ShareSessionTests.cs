using Mayfly.Web.Sharing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Mayfly.Web.Tests;

public class ShareSessionTests
{
    private const string Token = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private readonly ShareSession session = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Start_SetsScopedHttpOnlyCookie()
    {
        var cookie = StartSession(Guid.NewGuid());

        Assert.Equal(ShareSession.CookieName, cookie.Name.Value);
        Assert.Equal($"/s/{Token}", cookie.Path.Value);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal(ShareSession.Lifetime, cookie.MaxAge);
    }

    [Fact]
    public void IsActive_AcceptsCookieForTheSameLink()
    {
        var linkId = Guid.NewGuid();

        Assert.True(session.IsActive(RequestWith(StartSession(linkId).Value.Value), linkId));
    }

    [Fact]
    public void IsActive_RejectsCookieForAnotherLink()
    {
        var cookieValue = StartSession(Guid.NewGuid()).Value.Value;

        Assert.False(session.IsActive(RequestWith(cookieValue), Guid.NewGuid()));
    }

    [Fact]
    public void IsActive_RejectsTamperedOrMissingCookie()
    {
        var linkId = Guid.NewGuid();
        var cookieValue = StartSession(linkId).Value.Value!;
        var tampered = cookieValue[..^2] + (cookieValue[^2] == 'A' ? "BB" : "AA");

        Assert.False(session.IsActive(RequestWith(tampered), linkId));
        Assert.False(session.IsActive(new DefaultHttpContext(), linkId));
    }

    private SetCookieHeaderValue StartSession(Guid linkId)
    {
        var context = new DefaultHttpContext();
        session.Start(context, linkId, Token);
        return SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.ToString());
    }

    private static DefaultHttpContext RequestWith(string? cookieValue)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{ShareSession.CookieName}={cookieValue}";
        return context;
    }
}
