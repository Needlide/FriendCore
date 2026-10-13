using FriendCore.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class MessageReactionConfiguration : IEntityTypeConfiguration<MessageReaction>
{
    public void Configure(EntityTypeBuilder<MessageReaction> builder)
    {
        builder.ToTable("message_reactions");

        // Identity is (message, user, emoji): the same shape as Like's (post, user). It is what
        // makes a repeated tap on one emoji collide instead of duplicating, and it is what stops
        // a user holding two of the same reaction.
        builder.HasKey(reaction => new { reaction.MessageId, reaction.UserId, reaction.Emoji });

        // No surrogate Id, so unlike Message there is no Ulid key conversion here.
        builder.Property(reaction => reaction.MessageId)
            .HasColumnName("message_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(reaction => reaction.UserId)
            .HasColumnName("user_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(reaction => reaction.Emoji)
            .HasColumnName("emoji")
            .HasMaxLength(ReactionEmoji.MaxLength);

        builder.Property(reaction => reaction.CreatedAt).HasColumnName("created_at");

        // The composite PK already opens with message_id, so it serves the page's
        // "reactions for these messages" read with no extra index needed. This one covers the
        // opposite direction — everything a user has reacted to — for the future "your
        // reactions" / digest surface.
        builder.HasIndex(reaction => new { reaction.UserId, reaction.CreatedAt });
    }
}