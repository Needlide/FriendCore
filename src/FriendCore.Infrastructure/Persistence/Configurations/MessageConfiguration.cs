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

        // Nullable: a message may carry only an attachment. Existing rows are all text,
        // so the ALTER needs no backfill. The explicit text store type matters — left to
        // inference, the nullable conversion makes Npgsql map the column as a JSON
        // collection, which only surfaces when the column is used in an ANY(...) query.
        builder.Property(message => message.MediaItemId)
            .HasColumnName("media_item_id")
            .HasColumnType("text")
            .HasConversion(id => id == null ? null : id.ToString(), value => value == null ? null : Ulid.Parse(value));

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
        // constraints. The same value-conversion treatment as media_item_id, for the same
        // Npgsql reason: an explicit text column, because an inferred nullable Ulid conversion
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

        // Serves the "is this upload already attached?" probe. Partial, because a
        // text-only message has a null here and never needs the index.
        builder.HasIndex(message => message.MediaItemId)
            .HasFilter("media_item_id IS NOT NULL");
    }
}
