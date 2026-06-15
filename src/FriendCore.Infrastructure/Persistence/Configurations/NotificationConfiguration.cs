using FriendCore.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Id)
            .HasColumnName("id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(notification => notification.RecipientId)
            .HasColumnName("recipient_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(notification => notification.ActorId)
            .HasColumnName("actor_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(notification => notification.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(notification => notification.EntityId)
            .HasColumnName("entity_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(notification => notification.IsRead).HasColumnName("is_read");
        builder.Property(notification => notification.CreatedAt).HasColumnName("created_at");

        // Recipient's bell, newest first.
        builder.HasIndex(notification => new { notification.RecipientId, notification.CreatedAt, notification.Id });

        // Unread-count lookups: a partial index keeps it cheap as read notifications accumulate.
        builder.HasIndex(notification => notification.RecipientId)
            .HasFilter("is_read = false")
            .HasDatabaseName("ix_notifications_recipient_unread");
    }
}
