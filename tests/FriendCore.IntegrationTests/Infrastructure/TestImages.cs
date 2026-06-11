using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace FriendCore.IntegrationTests.Infrastructure;

public static class TestImages
{
    /// <summary>
    ///     A real, decodable PNG with an EXIF profile attached — processing must succeed on it and
    ///     the generated variants must come out stripped of that profile.
    /// </summary>
    public static byte[] RealPng(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(120, 180, 60));

        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Software, "friendcore-integration-tests");
        image.Metadata.ExifProfile = exif;

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
