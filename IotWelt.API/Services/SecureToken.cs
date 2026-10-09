using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace IotWelt.API.Services;

// Zufällige, URL-sichere Tokens und ihr SHA-256-Hash — gemeinsam genutzt von Refresh-Tokens
// und Einladungen. In der DB steht nur der Hash; das Token selbst verlässt die API genau einmal.
public static class SecureToken
{
    // 64 zufällige Bytes, Base64Url-kodiert (86 Zeichen, ohne + / =)
    public static string New() =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));

    // 64 Hex-Zeichen — passt zu den Spalten TokenHash (MaxLength 64)
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
