using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi;

public sealed record ReportEvidence(string SourceType, Guid SourceId, Guid AuthorId, string Content);

public interface IReportEvidenceSource
{
    string SourceType { get; }

    Task<ReportEvidence?> Find(HttpContext http, Guid sourceId, CancellationToken cancellationToken);
}
