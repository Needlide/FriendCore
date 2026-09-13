using FriendCore.Application.Common.Options;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace FriendCore.UnitTests.Users;

internal static class UserTestFactory
{
    public static readonly DateTimeOffset DefaultCreatedAt = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     The fixed pepper tests hash with — mirrors the real HMAC helper so factory users
    ///     carry deterministic, correctly-shaped email hashes.
    /// </summary>
    public const string TestPepper = "unit-tests-email-pepper";

    private static readonly HmacEmailHashService Hasher =
        new(Options.Create(new EmailHashOptions { Pepper = TestPepper }));

    public static string HashEmail(string email)
    {
        return Hasher.Hash(User.NormalizeEmail(email));
    }

    public static User Create(
        string username = "alice",
        string displayName = "Alice",
        string email = "alice@example.com",
        DateTimeOffset? createdAt = null)
    {
        var result = User.Register(
            username,
            displayName,
            HashEmail(email),
            email,
            "hashed-password",
            createdAt ?? DefaultCreatedAt);
        return result.Value;
    }

    public static User CreateVerified(DateTimeOffset? verifiedAt = null)
    {
        var user = Create();
        user.MarkEmailVerified(verifiedAt ?? DefaultCreatedAt);
        return user;
    }

    public static User CreateBanned()
    {
        var user = Create();
        user.Ban(DefaultCreatedAt);
        return user;
    }
}
