using FriendCore.Domain.Media;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class MediaRefusalConfiguration : IEntityTypeConfiguration<MediaRefusal>
{
    public void Configure(EntityTypeBuilder<MediaRefusal> builder)
    {
        builder.ToTable("media_refusals");

        // Identity is the whole triple, exactly as MessageReaction's is (message, user, emoji):
        // one refusal per (recipient, sender, content), so re-deleting cannot stack duplicates.
        builder.HasKey(refusal => new { refusal.RecipientId, refusal.SenderId, refusal.ContentHash });

        builder.Property(refusal => refusal.RecipientId)
            .HasColumnName("recipient_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(refusal => refusal.SenderId)
            .HasColumnName("sender_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        // Same length and case as MediaItem.ContentHash, so the two are comparable in a join and
        // a mismatch can never come from formatting.
        builder.Property(refusal => refusal.ContentHash)
            .HasColumnName("content_hash")
            .HasMaxLength(64);

        builder.Property(refusal => refusal.CreatedAt).HasColumnName("created_at");

        // The primary key already opens with recipient_id then sender_id, so it serves the only
        // query this table has: "has this sender's content been refused by this recipient".
        // The trailing index covers the opposite question for a future "your refusals" surface.
        builder.HasIndex(refusal => new { refusal.SenderId, refusal.CreatedAt });
    }
}
