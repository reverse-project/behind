using Microsoft.AspNetCore.Http;

namespace Behind.Core.WebApi.Middleware;

public sealed record SessionRejection(int StatusCode, string Reason)
{
    public static readonly SessionRejection MissingSession =
        new(StatusCodes.Status401Unauthorized, "The token is not bound to a session.");

    public static readonly SessionRejection UnknownSession =
        new(StatusCodes.Status401Unauthorized, "The session does not exist.");

    public static readonly SessionRejection Invalidated =
        new(StatusCodes.Status401Unauthorized, "The session has been invalidated.");

    public static readonly SessionRejection TokenMismatch =
        new(StatusCodes.Status401Unauthorized, "The token does not belong to the current session tokens.");

    public static readonly SessionRejection Expired =
        new(StatusCodes.Status401Unauthorized, "The session has expired.");
}
