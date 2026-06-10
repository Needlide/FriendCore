using FriendCore.Domain.Tags;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");

        builder.HasKey(tag => tag.Id);

        builder.Property(tag => tag.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.ToString(),
                value => Ulid.Parse(value));

        builder.Property(tag => tag.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(tag => tag.Slug)
            .HasColumnName("slug")
            .HasMaxLength(TagLimits.MaxSlugLength)
            .IsRequired();

        builder.HasIndex(tag => tag.Slug).IsUnique();
        builder.HasIndex(tag => tag.Slug).HasOperators("text_pattern_ops");

        builder.Property(tag => tag.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(tag => tag.Status);

        builder.Property(tag => tag.CreatedById)
            .HasColumnName("created_by_id")
            .HasConversion(
                id => id.ToString(),
                value => Ulid.Parse(value));

        builder.Property(tag => tag.MergedIntoId)
            .HasColumnName("merged_into_id")
            .HasConversion(
                id => id == null ? null : id.ToString(),
                value => value == null ? null : Ulid.Parse(value));

        builder.Property(tag => tag.LastUsedAt)
            .HasColumnName("last_used_at");

        // Serves the auto-archive sweep: status IN (...) AND profile_count < 3 AND last_used_at < cutoff.
        builder.HasIndex(tag => new { tag.Status, tag.LastUsedAt });

        builder.Property(tag => tag.ProfileCount)
            .HasColumnName("profile_count");

        builder.Property(tag => tag.PostCount)
            .HasColumnName("post_count");

        builder.Property(tag => tag.TotalCount)
            .HasColumnName("total_count");

        builder.HasIndex(tag => tag.TotalCount);

        builder.Property(tag => tag.CreatedAt)
            .HasColumnName("created_at");
    }
}