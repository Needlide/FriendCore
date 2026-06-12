using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("user_preferences");

        builder.HasKey(preferences => preferences.UserId);

        builder.Property(preferences => preferences.UserId)
            .HasColumnName("user_id")
            .HasConversion(
                id => id.ToString(),
                value => Ulid.Parse(value));

        builder.Property(preferences => preferences.HideFlashingMedia).HasColumnName("hide_flashing_media");
        builder.Property(preferences => preferences.ReduceMotion).HasColumnName("reduce_motion");
        builder.Property(preferences => preferences.DisableAutoplay).HasColumnName("disable_autoplay");
        builder.Property(preferences => preferences.ReduceVisualNoise).HasColumnName("reduce_visual_noise");
        builder.Property(preferences => preferences.ContentPreference)
            .HasColumnName("content_preference")
            .HasConversion<string>()
            .HasMaxLength(32);
        // Maps to a Postgres text[] column; field access keeps the domain's IReadOnlyList intact.
        builder.Property(preferences => preferences.KeywordFilters)
            .HasColumnName("keyword_filters")
            .HasField("_keywordFilters")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(preferences => preferences.CreatedAt).HasColumnName("created_at");
        builder.Property(preferences => preferences.UpdatedAt).HasColumnName("updated_at");
    }
}