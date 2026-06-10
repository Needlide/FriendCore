using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> builder)
    {
        builder.ToTable("follows");

        builder.HasKey(follow => new { follow.FollowerId, follow.FollowedId });

        builder.Property(follow => follow.FollowerId)
            .HasColumnName("follower_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(follow => follow.FollowedId)
            .HasColumnName("followed_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(follow => follow.CreatedAt)
            .HasColumnName("created_at");

        builder.HasIndex(follow => new { follow.FollowedId, follow.CreatedAt });
        builder.HasIndex(follow => new { follow.FollowerId, follow.CreatedAt });
    }
}