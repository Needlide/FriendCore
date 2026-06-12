using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> builder)
    {
        builder.ToTable("blocks");

        builder.HasKey(block => new { block.BlockerId, block.BlockedId });

        builder.Property(block => block.BlockerId)
            .HasColumnName("blocker_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(block => block.BlockedId)
            .HasColumnName("blocked_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(block => block.CreatedAt)
            .HasColumnName("created_at");

        // Either-direction checks (and the upcoming global query filter) probe both sides;
        // the PK covers blocker→blocked, this covers blocked→blocker.
        builder.HasIndex(block => new { block.BlockedId, block.BlockerId });
    }
}
