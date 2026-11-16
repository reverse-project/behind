using Behind.Common;
using Behind.Core.Database;

namespace Behind.Core.WebApi.Endpoints;

public static class SessionListResponseExtensions
{
    extension(ISessionQueries.SessionSummary summary)
    {
        private SessionListItem ToItem(Guid currentSessionId, DateTimeOffset? endedAt) =>
            new(
                summary.Id,
                summary.Id == currentSessionId,
                summary.StartedAt,
                summary.LastActiveAt,
                summary.ExpiresAt,
                endedAt,
                summary.SignedOut,
                summary.RemoteAddress,
                summary.UserAgent);
    }

    extension(PagedResult<ISessionQueries.SessionSummary> page)
    {
        public SessionPage ToActivePage(Guid currentSessionId) =>
            page.ToPage(summary => summary.ToItem(currentSessionId, null));

        public SessionPage ToEndedPage(Guid currentSessionId) =>
            page.ToPage(summary => summary.ToItem(currentSessionId, summary.SignedOutAt.GetOrElse(summary.ExpiresAt)));

        private SessionPage ToPage(Func<ISessionQueries.SessionSummary, SessionListItem> toItem) =>
            new(
                page.Total,
                page.Page,
                page.PageCount,
                page.PageSize,
                page.Results.Match(
                    () => (IReadOnlyList<SessionListItem>)[],
                    results => results.Select(toItem).ToList()));
    }

    extension(ISessionQueries.UserSessions sessions)
    {
        public SessionListResponse ToResponse(Guid currentSessionId) =>
            new(currentSessionId, sessions.Active.ToActivePage(currentSessionId), sessions.Ended.ToEndedPage(currentSessionId));
    }
}
