using FriendCore.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class MessageMediaConfiguration : IEntityTypeConfiguration<MessageMedia>
{
    public void Configure(EntityTypeBuilder<MessageMedia> builder)
    {
        builder.ToTable("message_media");

        builder.HasKey(messageMedia => new { messageMedia.MessageId, messageMedia.MediaItemId });

        builder.Property(messageMedia => messageMedia.MessageId)
            .HasColumnName("message_id")
            .HasColumnType("text")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(messageMedia => messageMedia.MediaItemId)
            .HasColumnName("media_item_id")
            .HasColumnType("text")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        // An image belongs to at most one message. Same rule as posts: the handler checks it,
        // and this makes the check unforgeable under a race.
        builder.HasIndex(messageMedia => messageMedia.MediaItemId).IsUnique();

        builder.Property(messageMedia => messageMedia.DisplayOrder).HasColumnName("display_order");
    }
}
