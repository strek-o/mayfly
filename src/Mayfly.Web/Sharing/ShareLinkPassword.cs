using Mayfly.Core.Data;
using Microsoft.AspNetCore.Identity;

namespace Mayfly.Web.Sharing;

public static class ShareLinkPassword
{
    private static readonly PasswordHasher<ShareLink> Hasher = new();

    public static string Hash(ShareLink link, string password) => Hasher.HashPassword(link, password);

    public static bool Verify(ShareLink link, string? password) =>
        link.PasswordHash is null
        || (password is not null
            && Hasher.VerifyHashedPassword(link, link.PasswordHash, password) != PasswordVerificationResult.Failed);
}
