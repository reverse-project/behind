using Behind.Common.WebApi;
using Behind.Forum.Common;
using Behind.Forum.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Forum.WebApi;

public sealed class ForumReportEvidenceSource(
    ForumQueries queries, IAuthorizationService authorization) : IReportEvidenceSource
{
    public string SourceType => "forumPost";

    public async Task<ReportEvidence?> Find(HttpContext http, Guid sourceId, CancellationToken cancellationToken)
    {
        if (!await authorization.HoldsGrantAsync(http.User, ForumNames.Read))
        {
            return null;
        }

        var post = await queries.PublicPost(sourceId, cancellationToken);
        return post is null ? null : new ReportEvidence(SourceType, sourceId, post.Entity.AuthorId, post.Entity.Body);
    }
}
