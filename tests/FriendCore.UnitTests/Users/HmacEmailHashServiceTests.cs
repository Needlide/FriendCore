using FriendCore.Application.Common.Options;
using FriendCore.Domain.Users;
using FriendCore.Infrastructure.Authentication;
using Microsoft.Extensions.Options;

namespace FriendCore.UnitTests.Users;

public class HmacEmailHashServiceTests
{
    private static HmacEmailHashService Create(string pepper = "pepper")
    {
        return new HmacEmailHashService(Options.Create(new EmailHashOptions { Pepper = pepper }));
    }

    [Fact]
    public void Hash_IsDeterministic_ForNormalizedInput()
    {
        var hasher = Create();

        Assert.Equal(hasher.Hash("alice@example.com"), hasher.Hash("alice@example.com"));
    }

    [Fact]
    public void Hash_MatchesUserNormalizeEmail_Output()
    {
        var hasher = Create();

        // Handler contract: Hash(User.NormalizeEmail(raw)) — leading/trailing case noise
        // must map to the same stored value.
        Assert.Equal(
            hasher.Hash(User.NormalizeEmail("  Alice@Example.com ")),
            hasher.Hash("alice@example.com"));
    }

    [Fact]
    public void Hash_Differs_AcrossPeppers_And_Addresses()
    {
        Assert.NotEqual(Create("one").Hash("alice@example.com"), Create("two").Hash("alice@example.com"));
        Assert.NotEqual(Create().Hash("alice@example.com"), Create().Hash("bob@example.com"));
    }

    [Fact]
    public void Hash_IsLowercaseHex_64Chars()
    {
        var hash = Create().Hash("alice@example.com");

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }
}
