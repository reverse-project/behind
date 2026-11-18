using Behind.Common.Configuration;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Oidc;
using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class OidcSettingsEndpoints
{
    public const string Route = "/settings/security.json/oidc";
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    [IgnoreETag]
    public static async Task<IResult> Get(ConfigurationFileCatalog catalog, HttpContext context,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        return await catalog.Find("security.json").Match(
            () => Task.FromResult<IResult>(Results.NotFound()),
            async file => Results.Json((await Read(file, cancellationToken)).ToOidcSettings()));
    }

    [IgnoreETag]
    public static async Task<IResult> Update(OidcSettings request, ConfigurationFileCatalog catalog,
        IOptions<SecurityConfiguration> security, ILogger<OidcSettingsEndpoints> logger,
        HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (security.Value.ReadOnlyConfiguration)
        {
            logger.LogWarning("OIDC settings update rejected: configuration is read-only");
            return Results.Json(ConfigurationForbiddenResponse.ConfigurationReadOnly, statusCode: StatusCodes.Status403Forbidden);
        }

        var target = catalog.Find("security.json").Match<FileInfo?>(() => null, file => file);
        if (target is null)
        {
            logger.LogWarning("OIDC settings update rejected: security.json is unavailable");
            return Results.NotFound();
        }

        await WriteGate.WaitAsync(cancellationToken);
        try
        {
            var stored = await Read(target, cancellationToken);
            OidcConfiguration applied;
            try
            {
                applied = request.ApplyOidcSettings(stored);
                _ = new OidcProviderCatalog(applied);
            }
            catch (Exception exception) when (exception is OptionsValidationException or ArgumentException)
            {
                logger.LogWarning("OIDC settings update rejected: invalid provider configuration ({FailureType})",
                    exception.GetType().Name);
                return Results.BadRequest();
            }

            var root = await target.ReadJsonAsync(cancellationToken);
            await target.SetJsonNodesAsync(new Dictionary<string, System.Text.Json.Nodes.JsonNode?>
            {
                [root.OidcSettingsPath()] = root.MergeOidcSettings(applied)
            }, cancellationToken);
            logger.LogInformation("OIDC settings saved with {ProviderCount} providers", applied.Providers.Length);
            return Results.NoContent();
        }
        catch (ConfigurationSchemaException)
        {
            logger.LogWarning("OIDC settings update rejected: configuration does not match its JSON schema");
            return Results.BadRequest();
        }
        finally
        {
            WriteGate.Release();
        }
    }

    private static async Task<OidcConfiguration> Read(FileInfo file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenRead();
        var configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        cancellationToken.ThrowIfCancellationRequested();
        return configuration.GetSection(OidcConfiguration.SectionName).Get<OidcConfiguration>() ?? new();
    }
}
