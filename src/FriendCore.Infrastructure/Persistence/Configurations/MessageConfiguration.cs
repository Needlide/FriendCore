using FriendCore.Domain.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(message => message.ConversationId)
            .HasColumnName("conversation_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(message => message.SenderId)
            .HasColumnName("sender_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(message => message.Content)
            .HasColumnName("content")
            .HasMaxLength(Message.MaxContentLength);

        builder.Property(message => message.IsEncrypted).HasColumnName("is_encrypted");

        builder.Property(message => message.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(message => message.CreatedAt).HasColumnName("created_at");
        builder.Property(message => message.DeletedAt).HasColumnName("deleted_at");
        builder.Property(message => message.EditedAt).HasColumnName("edited_at");

        // NOT a concurrency token (no IsConcurrencyToken): that would join the WHERE clause of
        // every update and delete on this row, letting a delete fail because the sender was
        // mid-rewrite on another device. The edit path opts in on its own via a compare-and-set
        // that carries this column in its WHERE and bumps it in the same statement.
        builder.Property(message => message.Version)
            .HasColumnName("version")
            .HasDefaultValue(0);

        // Nullable, and no FK — matching the module's deliberate absence of referential
        // constraints. An explicit text column, because an inferred nullable Ulid conversion
        // gets mapped as a JSON collection and breaks inside ANY(...).
        builder.Property(message => message.ReplyToMessageId)
            .HasColumnName("reply_to_message_id")
            .HasColumnType("text")
            .HasConversion(
                id => id == null ? null : id.ToString(),
                value => value == null ? null : Ulid.Parse(value));

        // Serves "load every parent this page replies to". A message with no reply target never
        // needs it, so the index is partial.
        builder.HasIndex(message => message.ReplyToMessageId)
            .HasFilter("reply_to_message_id IS NOT NULL");

        // Id trails the sort columns so the keyset (ORDER BY created_at DESC, id DESC) is fully
        // index-ordered even across messages that share a created_at.
        builder.HasIndex(message => new { message.ConversationId, message.CreatedAt, message.Id })
            .HasFilter("deleted_at IS NULL");

        // Auto-included from the field so a loaded message always knows its images; the
        // junction table itself is mapped by MessageMediaConfiguration.
        builder.HasMany(message => message.Media)
            .WithOne()
            .HasForeignKey(messageMedia => messageMedia.MessageId);

        builder.Navigation(message => message.Media).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
