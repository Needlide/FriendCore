using FriendCore.Domain.Moderation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class AppealConfiguration : IEntityTypeConfiguration<Appeal>
{
    public void Configure(EntityTypeBuilder<Appeal> builder)
    {
        builder.ToTable("appeals");

        builder.HasKey(appeal => appeal.Id);

        builder.Property(appeal => appeal.Id)
            .HasColumnName("id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(appeal => appeal.AuditLogId)
            .HasColumnName("audit_log_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(appeal => appeal.AppellantId)
            .HasColumnName("appellant_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(appeal => appeal.Reason).HasColumnName("reason").HasMaxLength(Appeal.MaxReasonLength);
        builder.Property(appeal => appeal.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(appeal => appeal.Deadline).HasColumnName("deadline");

        builder.Property(appeal => appeal.ReviewedByModeratorId)
            .HasColumnName("reviewed_by_moderator_id")
            .HasConversion(id => id == null ? null : id.ToString(), value => value == null ? null : Ulid.Parse(value));

        builder.Property(appeal => appeal.ReviewNote).HasColumnName("review_note").HasMaxLength(Appeal.MaxReviewNoteLength);
        builder.Property(appeal => appeal.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(appeal => appeal.CreatedAt).HasColumnName("created_at");

        // One appeal per action.
        builder.HasIndex(appeal => appeal.AuditLogId).IsUnique();

        // Queue: pending appeals by deadline.
        builder.HasIndex(appeal => new { appeal.Status, appeal.Deadline });
    }
}
