using System.Text.Json;
using FriendCore.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("media_items");

        builder.HasKey(media => media.Id);

        builder.Property(media => media.Id)
            .HasColumnName("id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(media => media.UploaderId)
            .HasColumnName("uploader_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.HasIndex(media => media.UploaderId).HasFilter("deleted_at IS NULL");

        builder.Property(media => media.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(media => media.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(media => media.OriginalStorageKey)
            .HasColumnName("original_storage_key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(media => media.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(media => media.SafetyFlags)
            .HasColumnName("safety_flags")
            .HasColumnType("jsonb")
            .HasConversion(
                flags => JsonSerializer.Serialize(
                    flags.Flags,
                    (JsonSerializerOptions?)null),
                json => new MediaSafetyFlags(
                    JsonSerializer.Deserialize<Dictionary<string, bool>>(
                        json,
                        (JsonSerializerOptions?)null)
                    ?? new Dictionary<string, bool>()),
                new ValueComparer<MediaSafetyFlags>(
                    (left, right) => SameFlags(left, right),
                    flags => flags.Flags.Aggregate(
                        0,
                        (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                    flags => new MediaSafetyFlags(
                        new Dictionary<string, bool>(flags.Flags))));

        builder.Property(media => media.SafetySource)
            .HasColumnName("safety_source")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(media => media.SafetyFlaggedAt).HasColumnName("safety_flagged_at");
        builder.Property(media => media.CreatedAt).HasColumnName("created_at");
        builder.Property(media => media.DeletedAt).HasColumnName("deleted_at");

        builder.OwnsMany(media => media.Variants, variant =>
        {
            variant.ToTable("media_variants");

            // Shadow FK: the variant has no domain-side back-reference to its owner.
            variant.Property<Ulid>("media_item_id")
                .HasColumnName("media_item_id")
                .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

            variant.WithOwner().HasForeignKey("media_item_id");
            variant.HasKey("media_item_id", nameof(MediaVariant.Type));

            variant.Property(v => v.Type)
                .HasColumnName("type")
                .HasConversion<string>()
                .HasMaxLength(16);

            variant.Property(v => v.StorageKey)
                .HasColumnName("storage_key")
                .HasMaxLength(200)
                .IsRequired();

            variant.Property(v => v.WidthPx).HasColumnName("width_px");
            variant.Property(v => v.HeightPx).HasColumnName("height_px");
            variant.Property(v => v.FileSizeBytes).HasColumnName("file_size_bytes");
        });

        builder.Navigation(media => media.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Nullable because it postdates the column: the bytes behind existing rows live in object
        // storage, not here, so they cannot be backfilled without re-reading every original. Null
        // therefore means "unknown", and every consumer must treat an unknown hash as
        // never-matching rather than as a shared one.
        builder.Property(media => media.ContentHash)
            .HasColumnName("content_hash")
            .HasMaxLength(64);

        // Serves "has this exact file been uploaded before?". Deliberately not partial on Status:
        // EF stores MediaStatus as its integer value, and filtering a partial index on another
        // column's state would bake that integer into the schema.
        builder.HasIndex(media => media.ContentHash);
    }

    /// <summary>
    ///     Structural equality for the JSON-backed dictionary. The record's generated equality
    ///     compares the dictionary by reference, so without this EF would treat two equal flag
    ///     sets as different values and write a pointless UPDATE on every save.
    /// </summary>
    private static bool SameFlags(MediaSafetyFlags? left, MediaSafetyFlags? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        if (left.Flags.Count != right.Flags.Count) return false;

        return left.Flags.All(pair =>
            right.Flags.TryGetValue(pair.Key, out var value) && value == pair.Value);
    }
}
