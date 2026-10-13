using FriendCore.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");

        builder.HasKey(conversation => conversation.Id);

        builder.Property(conversation => conversation.Id)
            .HasColumnName("id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(conversation => conversation.CreatedAt).HasColumnName("created_at");
        builder.Property(conversation => conversation.LastMessageAt).HasColumnName("last_message_at");

        // Most "list my conversations" queries sort by recency.
        builder.HasIndex(conversation => conversation.LastMessageAt);

        // The whole point of DirectKey: the database, not application code, is what stops two
        // simultaneous starts creating two conversations between the same two people. NULLs are
        // distinct in a Postgres unique index, so group conversations are unaffected by this.
        builder.Property(conversation => conversation.DirectKey).HasColumnName("direct_key");

        builder.HasIndex(conversation => conversation.DirectKey)
            .IsUnique()
            .HasFilter("direct_key IS NOT NULL")
            .HasDatabaseName("ix_conversations_direct_key");

        // Participants owned (auto-included), mirroring profile_tags on User.
        builder.OwnsMany(conversation => conversation.Participants, participant =>
        {
            participant.ToTable("conversation_participants");
            participant.WithOwner().HasForeignKey(p => p.ConversationId);
            participant.HasKey(p => new { p.ConversationId, p.UserId });

            participant.Property(p => p.ConversationId)
                .HasColumnName("conversation_id")
                .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

            participant.Property(p => p.UserId)
                .HasColumnName("user_id")
                .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

            // "Find the conversations a user is in" probes by user_id.
            participant.HasIndex(p => p.UserId);

            participant.Property(p => p.JoinedAt).HasColumnName("joined_at");
            participant.Property(p => p.LastReadAt).HasColumnName("last_read_at");
        });

        builder.Navigation(conversation => conversation.Participants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
