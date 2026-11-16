using Behind.Common;

namespace Behind.Core.Domain;

public sealed record UserReport(
    VersionedKey<Guid> Id,
    [property: Sensible] Guid ReporterId,
    [property: Sensible] Guid ConversationId,
    [property: Sensible] string TargetIds,
    [property: Sensible] string Reason,
    string SourceType,
    [property: Sensible] Guid SourceId,
    [property: Sensible] Guid? SourceAuthorId,
    [property: Sensible] string EvidenceContent,
    string Status,
    Guid? ResolvedById,
    string Resolution,
    [property: Sensible] string ReporterMessage,
    [property: Sensible] string ReportedEmail)
{
    private UserReport() : this(new(Guid.Empty), Guid.Empty, Guid.Empty, "", "", "", Guid.Empty, null, "", "open", null, "", "", "")
    {
    }

    public const int MaxReasonLength = 10_000;
    public const int MaxEvidenceLength = 100_000;
    public const int MaxTargets = 20;

    public IReadOnlyList<Guid> Users => TargetIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(Guid.Parse).ToArray();
}

public sealed record ReportEmailDelivery(
    VersionedKey<Guid> Id,
    [property: Sensible] Guid ReportId,
    [property: Sensible] Guid UserId,
    [property: Sensible] string Email,
    [property: Sensible] string Body,
    string Status)
{
    private ReportEmailDelivery() : this(new(Guid.Empty), Guid.Empty, Guid.Empty, "", "", "pending")
    {
    }
}
