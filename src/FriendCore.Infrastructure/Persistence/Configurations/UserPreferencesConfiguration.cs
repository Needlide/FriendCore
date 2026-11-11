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
        builder.Property(preferences => preferences.HideIntenseMotionMedia).HasColumnName("hide_intense_motion_media");
        builder.Property(preferences => preferences.HideArachnidMedia).HasColumnName("hide_arachnid_media");
        builder.Property(preferences => preferences.ReduceMotion).HasColumnName("reduce_motion");
        builder.Property(preferences => preferences.DisableAutoplay).HasColumnName("disable_autoplay");
        builder.Property(preferences => preferences.ReduceVisualNoise).HasColumnName("reduce_visual_noise");
        // Plain boolean, like its four siblings — no string conversion, no DB-level default.
        // Existing rows get true from the migration so they keep today's Enter-to-send behaviour.
        builder.Property(preferences => preferences.SendMessageOnEnter).HasColumnName("send_message_on_enter");
        // Maps to a Postgres text[] column; field access keeps the domain's IReadOnlyList intact.
        builder.Property(preferences => preferences.KeywordFilters)
            .HasColumnName("keyword_filters")
            .HasField("_keywordFilters")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(preferences => preferences.MutedNotificationTypes)
            .HasColumnName("muted_notification_types")
            .HasField("_mutedNotificationTypes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(preferences => preferences.SnoozedUntilUtc)
            .HasColumnName("snoozed_until_utc");

        builder.Property(preferences => preferences.CreatedAt).HasColumnName("created_at");
        builder.Property(preferences => preferences.UpdatedAt).HasColumnName("updated_at");
    }
}