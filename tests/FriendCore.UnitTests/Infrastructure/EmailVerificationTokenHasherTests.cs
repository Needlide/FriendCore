using FriendCore.Infrastructure.Email;

namespace FriendCore.UnitTests.Infrastructure;

public class EmailVerificationTokenHasherTests
{
    private readonly EmailVerificationTokenHasher _hasher = new();

    [Fact]
    public void Hash_IsDeterministic_ForSameInput()
    {
        Assert.Equal(_hasher.Hash("token"), _hasher.Hash("token"));
    }

    [Fact]
    public void Hash_ProducesLowercaseHexOf64Chars()
    {
        var hash = _hasher.Hash("token");

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void Hash_DiffersForDifferentInputs()
    {
        Assert.NotEqual(_hasher.Hash("token-a"), _hasher.Hash("token-b"));
    }

    [Fact]
    public void GenerateToken_ProducesUrlSafeValue()
    {
        var token = _hasher.GenerateToken();

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public void GenerateToken_ProducesUniqueValues()
    {
        Assert.NotEqual(_hasher.GenerateToken(), _hasher.GenerateToken());
    }
}
