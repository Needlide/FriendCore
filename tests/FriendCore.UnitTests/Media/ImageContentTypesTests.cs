using FriendCore.Application.Media;

namespace FriendCore.UnitTests.Media;

public class ImageContentTypesTests
{
    public static readonly TheoryData<string, byte[]> ValidHeaders = new()
    {
        { "image/jpeg", [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01] },
        { "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D] },
        { "image/gif", "GIF89a______"u8.ToArray() },
        { "image/webp", "RIFF\x01\x02\x03\x04WEBP"u8.ToArray() }
    };

    [Theory]
    [MemberData(nameof(ValidHeaders))]
    public void HeaderMatches_AcceptsCorrectSignature(string contentType, byte[] header)
    {
        Assert.True(ImageContentTypes.HeaderMatches(contentType, header));
    }

    [Fact]
    public void HeaderMatches_RejectsMismatchedSignature()
    {
        byte[] jpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

        Assert.False(ImageContentTypes.HeaderMatches("image/png", jpegHeader));
    }

    [Fact]
    public void HeaderMatches_RejectsTruncatedHeader()
    {
        byte[] tooShort = [0x89, 0x50];

        Assert.False(ImageContentTypes.HeaderMatches("image/png", tooShort));
        Assert.False(ImageContentTypes.HeaderMatches("image/webp", "RIFF"u8.ToArray()));
    }

    [Fact]
    public void HeaderMatches_RejectsUnknownContentType()
    {
        Assert.False(ImageContentTypes.HeaderMatches("text/plain", "GIF89a______"u8.ToArray()));
    }

    [Theory]
    [InlineData("image/jpeg", "jpg")]
    [InlineData("IMAGE/PNG", "png")]
    [InlineData("image/webp", "webp")]
    [InlineData("image/gif", "gif")]
    public void Extension_MapsContentType(string contentType, string expected)
    {
        Assert.Equal(expected, ImageContentTypes.Extension(contentType));
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/plain")]
    [InlineData("")]
    public void IsAllowed_RejectsUnsupportedTypes(string contentType)
    {
        Assert.False(ImageContentTypes.IsAllowed(contentType));
    }
}
