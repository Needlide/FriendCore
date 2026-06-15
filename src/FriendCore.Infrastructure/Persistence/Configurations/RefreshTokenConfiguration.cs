using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at");
        builder.Property(token => token.RevokedAt).HasColumnName("revoked_at");
        builder.Property(token => token.CreatedAt).HasColumnName("created_at");

        // Lookup-by-hash on every refresh.
        builder.HasIndex(token => token.TokenHash).IsUnique();

        // Bulk revoke for a user (logout-all / reuse detection / ban).
        builder.HasIndex(token => token.UserId);
    }
}
