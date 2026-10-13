using FriendCore.Domain.Moderation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FriendCore.Infrastructure.Persistence.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id)
            .HasColumnName("id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(report => report.ReporterId)
            .HasColumnName("reporter_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(report => report.TargetType)
            .HasColumnName("target_type")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(report => report.TargetId)
            .HasColumnName("target_id")
            .HasConversion(id => id.ToString(), value => Ulid.Parse(value));

        builder.Property(report => report.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(report => report.Priority)
            .HasColumnName("priority")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(report => report.Note).HasColumnName("note").HasMaxLength(Report.MaxNoteLength);
        builder.Property(report => report.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(report => report.ResolvedByModeratorId)
            .HasColumnName("resolved_by_moderator_id")
            .HasConversion(id => id == null ? null : id.ToString(), value => value == null ? null : Ulid.Parse(value));

        builder.Property(report => report.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(report => report.CreatedAt).HasColumnName("created_at");

        // Dedup probe: does this reporter already have a pending report on this target?
        builder.HasIndex(report => new { report.ReporterId, report.TargetType, report.TargetId, report.Status });

        // Queue scan: pending reports, priority then age. (status, priority, created_at)
        builder.HasIndex(report => new { report.Status, report.Priority, report.CreatedAt });

        // --- Subject snapshot ---------------------------------------------------------------
        // Deliberately NOT indexed and not searchable: this is moderator-only evidence, read one
        // row at a time from the queue. An index here would invite it being used for content
        // search, which is a different feature with different retention rules.

        builder.Property(report => report.SubjectKind)
            .HasColumnName("subject_kind")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(report => report.SubjectContent).HasColumnName("subject_content");

        builder.Property(report => report.SubjectAuthorId)
            .HasColumnName("subject_author_id")
            .HasConversion(id => id!.Value.ToString(), value => Ulid.Parse(value));

        builder.Property(report => report.SubjectAuthorUsername)
            .HasColumnName("subject_author_username")
            .HasMaxLength(64);

        builder.Property(report => report.SubjectCapturedAt).HasColumnName("subject_captured_at");
    }
}
