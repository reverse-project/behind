using System.Security.Claims;
using Behind.Core.Database;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.Helpers;

public static class SessionTestHelper
{
    public static DefaultHttpContext SignedIn(Guid sessionId) =>
        new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", sessionId.ToString())], "test"))
        };

    public static ISessionQueries SessionQueries(Guid sessionId, Guid userId, bool valid = true)
    {
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(sessionId, Arg.Any<CancellationToken>())
            .Returns(valid
                ? Some<(Session, Guid)>((
                    new ValidSession(
                        AccessToken.Create(new GuestUser(None)),
                        RefreshToken.Create(new GuestUser(None)),
                        "10.0.0.1",
                        "test-agent"),
                    userId))
                : None);

        return queries;
    }
}
