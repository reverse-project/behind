using static Behind.Common.FunctionalExtensions;
using Behind.Common;
using Behind.Common.Logging;
using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Core.Database;
using Behind.Core.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace Behind.Core.WebApi.Middleware;

public sealed class SessionValidationMiddleware(
    RequestDelegate next,
    TimeProvider timeProvider,
    ILogger<SessionValidationMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        ISessionQueries sessionQueries,
        IUserSettingQueries userSettings,
        LocaleCatalog locales,
        IConfiguration configuration)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await InvokeWithSessionContext(context, null, null, userSettings, locales, configuration);
            return;
        }

        var rejection = await FindRejectionAsync(context, sessionQueries);

        await rejection.Match(
            async () =>
            {
                var sessionId = SessionIdOf(context)!.Value;
                var actor = await sessionQueries.TryFindActor(sessionId, context.RequestAborted);
                var found = actor.Match(
                    () => throw new InvalidOperationException("The validated session no longer exists."),
                    value => value);
                await InvokeWithSessionContext(context, sessionId, found, userSettings, locales, configuration);
            },
            found =>
            {
                logger.LogWarning(
                    "Rejected request to {Path} for session {SessionId} with {StatusCode}: {Reason}",
                    () => [context.Request.Path.Value, SessionIdOf(context), found.StatusCode, found.Reason]);

                return context.Reject(found);
            });
    }

    private async Task InvokeWithSessionContext(
        HttpContext context,
        Guid? sessionId,
        ISessionQueries.SessionActor? actor,
        IUserSettingQueries userSettings,
        LocaleCatalog locales,
        IConfiguration configuration)
    {
        var available = await locales.ListAsync(context.RequestAborted);
        var (_, locale) = ContentLocaleMiddleware.SelectLocale(context, available);

        if (locale is null && actor is not null)
        {
            var settings = await userSettings.GetAll(actor.UserId, context.RequestAborted);
            if (settings.TryGetValue(UserSettingKeys.Locale, out var preferred))
            {
                locale = available.FirstOrDefault(item =>
                    string.Equals(item.Code, preferred, StringComparison.OrdinalIgnoreCase))?.Code;
            }
        }

        locale ??= configuration["Frontend:Site:DefaultLocale"];
        locale = string.IsNullOrWhiteSpace(locale) ? LocaleCatalog.Fallback : locale;
        var culture = CultureInfo.GetCultureInfo(locale);
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var impersonatorId = actor?.ImpersonatorId.Match(() => (Guid?)null, id => id);
        using var scope = SessionLogContext.Begin(new SessionLogInformation(culture.Name, sessionId, actor?.UserId, impersonatorId));
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            if (sessionId.HasValue)
            {
                logger.LogDebug(
                    "Session {SessionId} validated for request to {Path}",
                    () => [sessionId, context.Request.Path.Value]);
            }
            else
            {
                logger.LogDebug(
                    "Skipping session validation for anonymous request to {Path}",
                    () => [context.Request.Path.Value]);
            }
            await next(context);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            CultureInfo.CurrentUICulture = previousUiCulture;
        }
    }

    private static Guid? SessionIdOf(HttpContext context) =>
        context.User.SessionId.Match(() => (Guid?)null, id => id);

    private async Task<Option<SessionRejection>> FindRejectionAsync(
        HttpContext context,
        ISessionQueries sessionQueries) =>
        await context.User.SessionClaims.Match(
            () => Task.FromResult(Some(SessionRejection.MissingSession)),
            async claims =>
            {
                var session = await sessionQueries.TryFindById(claims.SessionId, context.RequestAborted);
                return session.Match(
                    () => Some(SessionRejection.UnknownSession),
                    found => found.RejectionFor(claims.Nonce, timeProvider.GetUtcNow()));
            });
}
