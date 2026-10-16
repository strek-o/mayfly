using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Mayfly.Web.Sharing;

public sealed class ShareTokenProtector(IDataProtectionProvider dataProtectionProvider)
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("Mayfly.ShareLinkToken");

    public string Protect(string token) => protector.Protect(token);

    public string? Unprotect(string? protectedToken)
    {
        if (protectedToken is null)
        {
            return null;
        }

        try
        {
            return protector.Unprotect(protectedToken);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
