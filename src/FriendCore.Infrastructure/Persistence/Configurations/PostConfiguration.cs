using FriendCore.Domain.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts");

        builder.HasKey(post => post.Id);

        builder.Property(post => post.Id)
            .HasColumnName("id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(post => post.AuthorId)
            .HasColumnName("author_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.HasIndex(post => post.AuthorId);

        builder.Property(post => post.Content)
            .HasColumnName("content")
            .HasMaxLength(PostErrors.MaxContentLength);

        builder.Property(post => post.Sensitivity)
            .HasColumnName("sensitivity")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(post => post.LikeCount).HasColumnName("like_count");
        builder.Property(post => post.CommentCount).HasColumnName("comment_count");
        builder.Property(post => post.CreatedAt).HasColumnName("created_at");
        builder.Property(post => post.UpdatedAt).HasColumnName("updated_at");
        builder.Property(post => post.DeletedAt).HasColumnName("deleted_at");
    }
}
