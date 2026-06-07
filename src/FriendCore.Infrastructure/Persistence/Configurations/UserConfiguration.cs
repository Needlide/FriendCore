using FriendCore.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.ToString(),
                value => Ulid.Parse(value));

        builder.Property(user => user.Username)
            .HasColumnName("username")
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(user => user.UsernameSlug)
            .HasColumnName("username_slug")
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(user => user.UsernameSlug).IsUnique();

        builder.Property(user => user.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(user => user.AvatarMediaId)
            .HasColumnName("avatar_media_id")
            .HasConversion(
                id => id == null ? null : id.ToString(),
                value => value == null ? null : Ulid.Parse(value));

        builder.Property(user => user.ContentPreference)
            .HasColumnName("content_preference")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(user => user.IsVerified)
            .HasColumnName("is_verified");

        builder.Property(user => user.IsBanned)
            .HasColumnName("is_banned");

        builder.Property(user => user.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(user => user.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(user => user.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(user => user.LastActiveAt)
            .HasColumnName("last_active_at");

        builder.OwnsOne(user => user.Profile, profile =>
        {
            profile.Property(p => p.Bio).HasColumnName("bio").HasMaxLength(500);
            profile.Property(p => p.WebsiteUrl).HasColumnName("website_url").HasMaxLength(500);
            profile.Property(p => p.Location).HasColumnName("location").HasMaxLength(200);
            profile.Property(p => p.UsernameLastChangedAt).HasColumnName("username_last_changed_at");
        });

        builder.OwnsMany(user => user.ProfileTags, profileTag =>
        {
            profileTag.ToTable("profile_tags");
            profileTag.WithOwner().HasForeignKey(tag => tag.UserId);
            profileTag.HasKey(tag => new { tag.UserId, tag.TagId });

            profileTag.Property(tag => tag.UserId)
                .HasColumnName("user_id")
                .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

            profileTag.Property(tag => tag.TagId)
                .HasColumnName("tag_id")
                .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

            profileTag.HasIndex(tag => tag.TagId);

            profileTag.Property(tag => tag.Tier)
                .HasColumnName("tier")
                .HasConversion<string>()
                .HasMaxLength(16);

            profileTag.Property(tag => tag.IsPublic).HasColumnName("is_public");
            profileTag.Property(tag => tag.AddedAt).HasColumnName("added_at");
        });

        builder.Navigation(user => user.ProfileTags).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}