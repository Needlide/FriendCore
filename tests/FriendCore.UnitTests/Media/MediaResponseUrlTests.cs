using FriendCore.Application.Common.Models;

namespace FriendCore.UnitTests.Media;

public class MediaResponseUrlTests
{
    [Theory]
    [InlineData("http://cdn.example/b", "k.webp", "http://cdn.example/b/k.webp")]
    [InlineData("http://cdn.example/b/", "k.webp", "http://cdn.example/b/k.webp")]
    [InlineData("http://cdn.example/b", "/k.webp", "http://cdn.example/b/k.webp")]
    [InlineData("http://cdn.example//b", "k.webp", "http://cdn.example/b/k.webp")]
    public void JoinUrl_NeverProducesDoubleSlashPaths(string baseUrl, string key, string expected)
    {
        Assert.Equal(expected, MediaResponse.JoinUrl(baseUrl, key));
    }
}
