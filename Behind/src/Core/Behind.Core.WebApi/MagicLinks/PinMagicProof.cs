using Behind.Common;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.MagicLinks;

public static class PinMagicProof
{
    public sealed record RequestBody(string Operation);

    private static string Purpose(Guid actorId, Guid targetId, string operation) =>
        $"switch-pin/{actorId:N}/{targetId:N}/{operation}";

    [Authorize, IgnoreETag]
    public static async Task<IResult> Request(Guid userId, RequestBody request, HttpContext http, ISessionQueries sessions,
        AbstractCoreContext db, UserManager<ApplicationUser> users, IConfiguration configuration, IMagicLinkSettingsStore store,
        IOptions<SecurityConfiguration> security, IMagicLinkCommands commands, IMagicLinkQueries queries,
        IMagicLinkMailQueue mailQueue, TimeProvider clock)
    {
        http.Response.Headers.CacheControl = "no-store";
        if (request.Operation is not ("change" or "remove" or "create"))
        {
            return Results.BadRequest();
        }

        var actor = await SubaccountEndpoints.GetPinManager(userId, http, sessions, db);
        if (actor is not { } actorId || (request.Operation == "create" && userId != actorId))
        {
            return Results.Forbid();
        }

        var user = await users.FindByIdAsync(actorId.ToString());
        var address = user?.Emails.FirstOrDefault(email => email.ConfirmedAt is not null);
        var settings = store.Current.Match<MagicLinkSettings?>(() => null, value => value);
        var frontend = configuration.FrontendUri().Match<Uri?>(() => null, value => value);
        var jwt = security.Value.Jwt.Match<JwtSettings?>(() => null, value => value);
        if (user is null || address is null || settings is null || jwt is null || frontend is null)
        {
            return Results.BadRequest();
        }

        var now = clock.GetUtcNow();
        if (await queries.HasIssuedSince(user.Id, now - settings.EffectiveRequestCooldown, http.RequestAborted))
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        var link = MagicLink.Create(now, settings.EffectiveLinkLifetime, MagicLinkFunction.SwitchPin);
        var token = MagicLinkCredential.Create(link.Nonce, settings.ActualSecret, jwt.SigningKey,
            Purpose(user.Id, userId, request.Operation)).Token;
        var url = frontend.ToFrontendLink("/profile/pin-proof",
            [new("pinProof", token), new("pinTarget", userId.ToString()), new("pinOperation", request.Operation)]);
        await commands.Issue(user.Id, link, http.RequestAborted);
        return mailQueue.TryEnqueue(new MagicLinkMail(user, address.Value, url, MagicLinkFunction.SwitchPin))
            ? Results.NoContent()
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    public static async Task<bool> Consume(string token, Guid actorId, Guid targetId, string operation, HttpContext http)
    {
        var services = http.RequestServices;
        if (!services.GetRequiredService<IOptionsSnapshot<AccountsConfiguration>>().Value.MagicLink)
        {
            return false;
        }
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var nonce =
            from settings in services.GetRequiredService<IMagicLinkSettingsStore>().Current
            from jwt in services.GetRequiredService<IOptions<SecurityConfiguration>>().Value.Jwt
            from credential in MagicLinkCredential.TryParse(token)
            where credential.IsSignedBy(settings.ValidSecrets(now), jwt.SigningKey, Purpose(actorId, targetId, operation))
            select credential.Nonce;
        return await nonce.Match(() => Task.FromResult(false), value =>
            services.GetRequiredService<IMagicLinkCommands>().ConsumeForFunction(
                value, actorId, MagicLinkFunction.SwitchPin, now, http.RequestAborted));
    }
}
