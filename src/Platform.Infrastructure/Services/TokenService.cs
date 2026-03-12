using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Platform.Application.Abstractions;

namespace Platform.Infrastructure.Services;

public sealed class TokenService : ITokenService
{
    public IssuedTokenPair CreateTokenPair(TimeSpan accessLifetime, TimeSpan refreshLifetime)
    {
        var now = DateTimeOffset.UtcNow;
        var accessToken = CreateToken();
        var refreshToken = CreateToken();

        return new IssuedTokenPair(
            accessToken,
            HashToken(accessToken),
            now.Add(accessLifetime),
            refreshToken,
            HashToken(refreshToken),
            now.Add(refreshLifetime));
    }

    public string HashToken(string rawToken)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hash);
    }

    private static string CreateToken()
    {
        Span<byte> buffer = stackalloc byte[48];
        RandomNumberGenerator.Fill(buffer);
        return WebEncoders.Base64UrlEncode(buffer);
    }
}
