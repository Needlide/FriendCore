using FriendCore.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class MessageHiddenConfiguration : IEntityTypeConfiguration<MessageHidden>
{
    public void Configure(EntityTypeBuilder<MessageHidden> builder)
    {
        builder.ToTable("message_hidden");

        // Identity is (message, user) — one participant's opinion about one message, and hiding
        // twice is a no-op rather than a duplicate. Same shape as MessageReaction's composite key.
        builder.HasKey(hidden => new { hidden.MessageId, hidden.UserId });

        builder.Property(hidden => hidden.MessageId)
            .HasColumnName("message_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(hidden => hidden.UserId)
            .HasColumnName("user_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(hidden => hidden.HiddenAt).HasColumnName("hidden_at");

        // Serves "which of this page's messages has this participant hidden?" — the composite
        // key opens with message_id, so it covers the per-page filter directly.
        builder.HasIndex(hidden => new { hidden.UserId, hidden.MessageId });
    }
}
