using System.Security.Cryptography;
using System.Text;
using FriendCore.Application.Common.Interfaces;

namespace FriendCore.Infrastructure.Email;

public sealed class EmailVerificationTokenHasher : IEmailVerificationTokenHasher
{
    public string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public string Hash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
