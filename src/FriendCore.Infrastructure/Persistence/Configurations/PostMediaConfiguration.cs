using FriendCore.Domain.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class PostMediaConfiguration : IEntityTypeConfiguration<PostMedia>
{
    public void Configure(EntityTypeBuilder<PostMedia> builder)
    {
        builder.ToTable("post_media");

        builder.HasKey(postMedia => new { postMedia.PostId, postMedia.MediaItemId });

        builder.Property(postMedia => postMedia.PostId)
            .HasColumnName("post_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(postMedia => postMedia.MediaItemId)
            .HasColumnName("media_item_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        // A media item belongs to at most one post. Avatar usage references media via
        // users.avatar_media_id and is not constrained by this.
        builder.HasIndex(postMedia => postMedia.MediaItemId).IsUnique();

        builder.Property(postMedia => postMedia.DisplayOrder).HasColumnName("display_order");
    }
}
