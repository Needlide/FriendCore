using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class MuteConfiguration : IEntityTypeConfiguration<Mute>
{
    public void Configure(EntityTypeBuilder<Mute> builder)
    {
        builder.ToTable("mutes");

        builder.HasKey(mute => new { mute.MuterId, mute.MutedId });

        builder.Property(mute => mute.MuterId)
            .HasColumnName("muter_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(mute => mute.MutedId)
            .HasColumnName("muted_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(mute => mute.CreatedAt)
            .HasColumnName("created_at");

        // No reverse index: mutes are one-way, every lookup starts from the muter — the PK covers it.
    }
}
