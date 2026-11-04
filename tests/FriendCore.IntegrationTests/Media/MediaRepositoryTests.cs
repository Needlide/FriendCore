using FriendCore.Domain.Media;
using FriendCore.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FriendCore.IntegrationTests.Media;

/// <summary>
///     Proves the media EF mapping against real Postgres: owned variant collection, enum/Ulid
///     conversions, and the soft-delete query filter.
/// </summary>
public class MediaRepositoryTests(FriendCoreApiFactory factory) : IClassFixture<FriendCoreApiFactory>
{
    private static readonly DateTimeOffset Now = new(2026, 6, 11, 9, 0, 0, TimeSpan.Zero);

    private static MediaItem NewItem()
    {
        return MediaItem.Create(
            Ulid.NewUlid(), "photo.jpg", "image/jpeg", "jpg",
            MediaSafetyFlags.From(("flashing", true)), Now);
    }

    [Fact]
    public async Task Add_ThenGet_RoundTripsItem()
    {
        var media = NewItem();

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMediaRepository>().AddAsync(media);
        }

        using var readScope = factory.Services.CreateScope();
        var loaded = await readScope.ServiceProvider.GetRequiredService<IMediaRepository>()
            .GetByIdAsync(media.Id);

        Assert.NotNull(loaded);
        Assert.Equal(media.UploaderId, loaded.UploaderId);
        Assert.Equal(media.OriginalStorageKey, loaded.OriginalStorageKey);
        Assert.Equal(MediaStatus.Processing, loaded.Status);
        Assert.Equal(MediaSafetySource.UserSelfMarked, loaded.SafetySource);
        Assert.True(loaded.SafetyFlags.IsSet("flashing"));
        Assert.Empty(loaded.Variants);
    }

    [Fact]
    public async Task MarkReady_PersistsOwnedVariants()
    {
        var media = NewItem();

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IMediaRepository>();
            await repository.AddAsync(media);

            media.MarkReady(
            [
                MediaVariant.Create(
                    MediaVariantType.Thumbnail, MediaStorageKeys.Variant(media.Id, MediaVariantType.Thumbnail),
                    100, 100, 2_048),
                MediaVariant.Create(
                    MediaVariantType.Large, MediaStorageKeys.Variant(media.Id, MediaVariantType.Large),
                    1400, 900, 350_000)
            ]);
            await repository.UpdateAsync();
        }

        using var readScope = factory.Services.CreateScope();
        var loaded = await readScope.ServiceProvider.GetRequiredService<IMediaRepository>()
            .GetByIdAsync(media.Id);

        Assert.NotNull(loaded);
        Assert.True(loaded.IsReady);
        Assert.Equal(2, loaded.Variants.Count);

        var large = Assert.Single(loaded.Variants, v => v.Type == MediaVariantType.Large);
        Assert.Equal($"uploads/variants/{media.Id}/large.webp", large.StorageKey);
        Assert.Equal(1400, large.WidthPx);
        Assert.Equal(350_000, large.FileSizeBytes);
    }

    [Fact]
    public async Task SoftDelete_HidesItem_ViaQueryFilter()
    {
        var media = NewItem();

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IMediaRepository>();
            await repository.AddAsync(media);
            await repository.SoftDeleteAsync(media);
        }

        using var readScope = factory.Services.CreateScope();
        var loaded = await readScope.ServiceProvider.GetRequiredService<IMediaRepository>()
            .GetByIdAsync(media.Id);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task GetByIds_ReturnsOnlyRequestedItems()
    {
        var first = NewItem();
        var second = NewItem();
        var unrelated = NewItem();

        using (var scope = factory.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IMediaRepository>();
            await repository.AddAsync(first);
            await repository.AddAsync(second);
            await repository.AddAsync(unrelated);
        }

        using var readScope = factory.Services.CreateScope();
        var loaded = await readScope.ServiceProvider.GetRequiredService<IMediaRepository>()
            .GetByIdsAsync([first.Id, second.Id]);

        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, m => m.Id == first.Id);
        Assert.Contains(loaded, m => m.Id == second.Id);
    }
}
