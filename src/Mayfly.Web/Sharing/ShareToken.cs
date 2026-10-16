using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Mayfly.Web.Sharing;

public static class ShareToken
{
    private const int TokenBytes = 32;

    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));

    public static bool IsWellFormed(string? token) =>
        token is not null
        && Base64Url.IsValid(token, out var decodedLength)
        && decodedLength == TokenBytes;

    public static byte[] Hash(string token) => SHA256.HashData(Encoding.ASCII.GetBytes(token));
}
