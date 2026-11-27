using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Mcp;

public sealed class McpSessionQueries(SessionQueries sessions, IHttpContextAccessor accessor) : ISessionQueries
{
    private Option<ServiceUserIdentity> Identity(Guid id) =>
        accessor.HttpContext?.Features.Get<ServiceUserIdentity>() is { } identity && identity.SessionId == id
            ? Some(identity)
            : None;

    public Task<Option<Session>> TryFindById(Guid id, CancellationToken cancellationToken = default) =>
        Identity(id).Match(
            () => sessions.TryFindById(id, cancellationToken),
            identity => Task.FromResult(Some<Session>(identity.Session)));

    public Task<Option<(Session Session, Guid UserId)>> TryFindByIdWithUserId(
        Guid id, CancellationToken cancellationToken = default) =>
        Identity(id).Match(
            () => sessions.TryFindByIdWithUserId(id, cancellationToken),
            identity => Task.FromResult(Some<(Session, Guid)>((identity.Session, identity.ServiceUser.Id))));

    public Task<Option<ISessionQueries.SessionActor>> TryFindActor(Guid id, CancellationToken cancellationToken = default) =>
        Identity(id).Match(
            () => sessions.TryFindActor(id, cancellationToken),
            identity => Task.FromResult(Some(new ISessionQueries.SessionActor(identity.ServiceUser.Id, None))));

    public Task<ISessionQueries.UserSessions> GetUserSessions(
        Guid userId, DateTimeOffset now, Flipper active, Flipper ended, CancellationToken cancellationToken = default) =>
        sessions.GetUserSessions(userId, now, active, ended, cancellationToken);
}
