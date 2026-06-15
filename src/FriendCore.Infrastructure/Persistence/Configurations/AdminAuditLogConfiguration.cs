using FriendCore.Domain.Moderation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("admin_audit_log");

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(entry => entry.ModeratorId)
            .HasColumnName("moderator_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(entry => entry.Action)
            .HasColumnName("action")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(entry => entry.TargetType)
            .HasColumnName("target_type")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(entry => entry.TargetId)
            .HasColumnName("target_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(entry => entry.Reason).HasColumnName("reason").HasMaxLength(AdminAuditLog.MaxReasonLength);
        builder.Property(entry => entry.IpAddress).HasColumnName("ip_address").HasMaxLength(64);

        builder.Property(entry => entry.RelatedReportId)
            .HasColumnName("related_report_id")
            .HasConversion(id => id == null ? null : id.ToString(), value => value == null ? null : Ulid.Parse(value));

        builder.Property(entry => entry.CreatedAt).HasColumnName("created_at");

        // Recent-first global log.
        builder.HasIndex(entry => entry.CreatedAt);

        // Per-user moderation history: entries targeting a user, newest first.
        builder.HasIndex(entry => new { entry.TargetType, entry.TargetId, entry.CreatedAt });
    }
}
