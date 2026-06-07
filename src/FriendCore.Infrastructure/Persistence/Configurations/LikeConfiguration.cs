using FriendCore.Domain.Posts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class LikeConfiguration : IEntityTypeConfiguration<Like>
{
    public void Configure(EntityTypeBuilder<Like> builder)
    {
        builder.ToTable("likes");

        builder.HasKey(like => new { like.PostId, like.UserId });

        builder.Property(like => like.PostId)
            .HasColumnName("post_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(like => like.UserId)
            .HasColumnName("user_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.HasIndex(like => like.UserId);

        builder.Property(like => like.CreatedAt).HasColumnName("created_at");
    }
}
