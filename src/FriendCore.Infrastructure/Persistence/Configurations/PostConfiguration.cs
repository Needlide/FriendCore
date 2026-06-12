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

        builder.HasIndex(post => post.AuthorId).HasFilter("deleted_at IS NULL");

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

        // Id trails the sort columns so the keyset (ORDER BY created_at DESC, id DESC) is fully
        // index-ordered even across rows that share a created_at.
        builder.HasIndex(post => new { post.AuthorId, post.CreatedAt, post.Id }).HasFilter("deleted_at IS NULL");
        builder.HasIndex(post => new { post.CreatedAt, post.Id }).HasFilter("deleted_at IS NULL");

        builder.HasMany(post => post.Media)
            .WithOne()
            .HasForeignKey(postMedia => postMedia.PostId);

        builder.Navigation(post => post.Media).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Owned (auto-included), mirroring profile_tags on User.
        builder.OwnsMany(post => post.Tags, postTag =>
        {
            postTag.ToTable("post_tags");
            postTag.WithOwner().HasForeignKey(tag => tag.PostId);
            postTag.HasKey(tag => new { tag.PostId, tag.TagId });

            postTag.Property(tag => tag.PostId)
                .HasColumnName("post_id")
                .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

            postTag.Property(tag => tag.TagId)
                .HasColumnName("tag_id")
                .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

            // Discovery scans posts by tag: (tag_id, post_id) makes that an index-only probe.
            postTag.HasIndex(tag => new { tag.TagId, tag.PostId });

            postTag.Property(tag => tag.AddedAt).HasColumnName("added_at");
        });

        builder.Navigation(post => post.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}