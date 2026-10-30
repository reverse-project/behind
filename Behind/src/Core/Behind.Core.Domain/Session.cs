using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Domain;

public abstract record Session;

public sealed record ValidSession(
    User User,
    AccessToken AccessToken,
    RefreshToken RefreshToken,
    RemoteAddress RemoteAddress,
    UserAgent UserAgent
) : Session
{
    private ValidSession() : this(new GuestUser(None), default!, default!, default!, default!)
    {
    }

    public ValidSession(
        AccessToken accessToken,
        RefreshToken refreshToken,
        RemoteAddress remoteAddress,
        UserAgent userAgent
    ) : this(new GuestUser(None), accessToken, refreshToken, remoteAddress, userAgent)
    {
    }
}

public sealed record InvalidSession(Option<User> InvalidatedBy, Option<DateTimeOffset> InvalidatedAt) : Session;
