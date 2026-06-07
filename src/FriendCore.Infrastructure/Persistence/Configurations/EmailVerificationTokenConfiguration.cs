using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("email_verification_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.ToString(),
                value => Ulid.Parse(value));

        builder.Property(token => token.UserId)
            .HasColumnName("user_id")
            .HasConversion(
                id => id.ToString(),
                value => Ulid.Parse(value));

        builder.HasIndex(token => token.UserId);

        builder.Property(token => token.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(token => token.TokenHash).IsUnique();

        builder.Property(token => token.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(token => token.ConsumedAt)
            .HasColumnName("consumed_at");

        builder.Property(token => token.CreatedAt)
            .HasColumnName("created_at");
    }
}