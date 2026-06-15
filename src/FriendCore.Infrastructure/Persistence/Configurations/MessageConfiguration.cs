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
            .HasMaxLength(Message.MaxContentLength)
            .IsRequired();

        builder.Property(message => message.IsEncrypted).HasColumnName("is_encrypted");

        builder.Property(message => message.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(message => message.CreatedAt).HasColumnName("created_at");
        builder.Property(message => message.DeletedAt).HasColumnName("deleted_at");

        // Id trails the sort columns so the keyset (ORDER BY created_at DESC, id DESC) is fully
        // index-ordered even across messages that share a created_at.
        builder.HasIndex(message => new { message.ConversationId, message.CreatedAt, message.Id })
            .HasFilter("deleted_at IS NULL");
    }
}
