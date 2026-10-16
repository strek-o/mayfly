using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Mayfly.Web.Sharing;

public sealed class ShareSession(IDataProtectionProvider dataProtectionProvider)
{
    public const string CookieName = "mayfly-share";

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private readonly ITimeLimitedDataProtector protector = dataProtectionProvider
        .CreateProtector("Mayfly.ShareSession")
        .ToTimeLimitedDataProtector();

    public void Start(HttpContext context, Guid linkId, string token) =>
        context.Response.Cookies.Append(
            CookieName,
            protector.Protect(linkId.ToString("N"), Lifetime),
            new CookieOptions
            {
                Path = $"/s/{token}",
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = Lifetime,
                IsEssential = true
            });

    public bool IsActive(HttpContext context, Guid linkId)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var value))
        {
            return false;
        }

        try
        {
            return protector.Unprotect(value) == linkId.ToString("N");
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
