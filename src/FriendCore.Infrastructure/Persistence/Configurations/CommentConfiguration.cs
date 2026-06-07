using FriendCore.Domain.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("comments");

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Id)
            .HasColumnName("id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(comment => comment.PostId)
            .HasColumnName("post_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.HasIndex(comment => comment.PostId);

        builder.Property(comment => comment.AuthorId)
            .HasColumnName("author_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(comment => comment.Content)
            .HasColumnName("content")
            .HasMaxLength(Comment.MaxContentLength)
            .IsRequired();

        builder.Property(comment => comment.CreatedAt).HasColumnName("created_at");
        builder.Property(comment => comment.DeletedAt).HasColumnName("deleted_at");
    }
}
