using Mayfly.Web.Sharing;
using Microsoft.AspNetCore.DataProtection;

namespace Mayfly.Web.Tests;

public class ShareTokenProtectorTests
{
    private readonly ShareTokenProtector protector = new(new EphemeralDataProtectionProvider());

    [Fact]
    public void ProtectedToken_RoundTripsAndDoesNotContainTheToken()
    {
        var token = ShareToken.Generate();

        var protectedToken = protector.Protect(token);

        Assert.DoesNotContain(token, protectedToken);
        Assert.Equal(token, protector.Unprotect(protectedToken));
    }

    [Fact]
    public void Unprotect_ReturnsNullForMissingOrTamperedValue()
    {
        var protectedToken = protector.Protect(ShareToken.Generate());
        var tampered = protectedToken[..^2] + (protectedToken[^2] == 'A' ? "BB" : "AA");

        Assert.Null(protector.Unprotect(null));
        Assert.Null(protector.Unprotect(tampered));
    }
}
