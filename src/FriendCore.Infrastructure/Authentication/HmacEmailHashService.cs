using System.Security.Cryptography;
using System.Text;
using FriendCore.Application.Common.Interfaces;
using FriendCore.Application.Common.Options;
using Microsoft.Extensions.Options;

namespace FriendCore.Infrastructure.Authentication;

public sealed class HmacEmailHashService(IOptions<EmailHashOptions> options) : IEmailHashService
{
    private readonly byte[] _pepper = Encoding.UTF8.GetBytes(options.Value.Pepper);

    public string Hash(string normalizedEmail)
    {
        var hash = HMACSHA256.HashData(_pepper, Encoding.UTF8.GetBytes(normalizedEmail));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
