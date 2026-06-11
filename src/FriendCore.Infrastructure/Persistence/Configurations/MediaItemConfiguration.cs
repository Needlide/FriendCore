using FriendCore.Domain.Media;
using Microsoft.EntityFrameworkCore;
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

        builder.Property(media => media.HasFlashingWarning).HasColumnName("has_flashing_warning");
        builder.Property(media => media.HasIntenseMotionWarning).HasColumnName("has_intense_motion_warning");

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
    }
}
