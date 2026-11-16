using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Behind.Core.Database.Aggregates;

public sealed class UserReportConfiguration : IEntityTypeConfiguration<UserReport>
{
    public void Configure(EntityTypeBuilder<UserReport> builder)
    {
        builder.ToTable("UserReports");
        builder.HasVersionedKey();
        builder.Ignore(report => report.Users);
        builder.Property(report => report.TargetIds).HasMaxLength(UserReport.MaxTargets * 37).IsRequired();
        builder.Property(report => report.Reason).HasMaxLength(UserReport.MaxReasonLength).IsRequired();
        builder.Property(report => report.EvidenceContent).HasMaxLength(UserReport.MaxEvidenceLength).IsRequired();
        builder.Property(report => report.SourceType).HasMaxLength(40).IsRequired();
        builder.Property(report => report.Status).HasMaxLength(16).IsRequired();
        builder.Property(report => report.Resolution).HasMaxLength(16).IsRequired();
        builder.Property(report => report.ReporterMessage).HasMaxLength(UserReport.MaxReasonLength).IsRequired();
        builder.Property(report => report.ReportedEmail).HasMaxLength(UserReport.MaxReasonLength).IsRequired();
        builder.HasIndex(report => report.ReporterId);
        builder.HasIndex(report => report.ConversationId);
        builder.HasIndex(report => report.Status);
    }
}

public sealed class ReportEmailDeliveryConfiguration : IEntityTypeConfiguration<ReportEmailDelivery>
{
    public void Configure(EntityTypeBuilder<ReportEmailDelivery> builder)
    {
        builder.ToTable("ReportEmailDeliveries");
        builder.HasVersionedKey();
        builder.Property(delivery => delivery.Email).HasMaxLength(320).IsRequired();
        builder.Property(delivery => delivery.Body).HasMaxLength(UserReport.MaxReasonLength).IsRequired();
        builder.Property(delivery => delivery.Status).HasMaxLength(16).IsRequired();
        builder.HasIndex(delivery => delivery.ReportId);
    }
}
