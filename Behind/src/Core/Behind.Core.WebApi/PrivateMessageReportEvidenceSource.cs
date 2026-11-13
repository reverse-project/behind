using Behind.Common.WebApi;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi;

public sealed class PrivateMessageReportEvidenceSource(
    PrivateMessageStore store, IModuleIdentity identity, IAuthorizationService authorization) : IReportEvidenceSource
{
    public string SourceType => "privateMessage";

    public async Task<ReportEvidence?> Find(HttpContext http, Guid sourceId, CancellationToken cancellationToken)
    {
        var viewer = await identity.UserId(http);
        if (viewer is null
            || !await authorization.HoldsGrantAsync(http.User, GrantName.Of(GrantArea.PrivateMessage, GrantAction.Read)))
        {
            return null;
        }

        var systemModerator = await authorization.HoldsGrantAsync(http.User,
            GrantName.Of(GrantArea.PrivateMessageModeration, GrantAction.Manage));
        var reportModerator = await authorization.HoldsGrantAsync(http.User, GrantName.Of(GrantArea.Report, GrantAction.Manage));
        var evidence = await store.FindEvidence(viewer.Value, sourceId, systemModerator, reportModerator, cancellationToken);
        return evidence is null ? null : new(SourceType, sourceId, evidence.AuthorId, evidence.Content);
    }
}
