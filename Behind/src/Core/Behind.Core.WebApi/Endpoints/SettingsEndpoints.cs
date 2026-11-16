using Behind.Common;
using Behind.Common.Configuration;
using Behind.Common.Logging;
using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.MagicLinks;
using Behind.Core.WebApi.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SettingsEndpoints
{
    public static async Task<IResult> Get(
        ConfigurationFileCatalog catalog,
        LocaleCatalog locales,
        IOptions<SecurityConfiguration> security,
        CancellationToken cancellationToken)
    {
        var available = await locales.ListAsync(cancellationToken);
        var files = await Task.WhenAll(
            catalog.Files.Select(file => catalog.EditorOf(file).Match(
                () => file.ToSettingsFile(available, cancellationToken),
                editor => Task.FromResult(new SettingsFileResponse(file.Name, [], editor)))));

        return Results.Json(new SettingsResponse(security.Value.ReadOnlyConfiguration, files));
    }

    public static Task<IResult> Update(
        string file,
        SettingsUpdateRequest request,
        ConfigurationFileCatalog catalog,
        LocaleCatalog locales,
        IOptions<SecurityConfiguration> security,
        ILogger<SettingsEndpoints> logger,
        CancellationToken cancellationToken)
    {
        var target = catalog.Find(file);
        if (target == None)
        {
            return catalog.FindWithEditor(file, ConfigurationFileCatalog.LoggingEditor).Match(
                () => Task.FromResult<IResult>(Results.NotFound()),
                appSettings => Modify(
                    Some(appSettings),
                    security,
                    targetFile => UpdateAppSettings(targetFile, request, logger, cancellationToken)));
        }

        if (request.Logging is not null || request.OpenTelemetry is not null)
        {
            return Task.FromResult<IResult>(Results.BadRequest());
        }

        return Modify(
            target,
            security,
            async configurationFile =>
            {
                var changes = (await configurationFile.ReadJsonAsync(cancellationToken))
                    .PlanChanges(configurationFile.Name, request.Values);

                return await changes.Match(
                    () => Task.FromResult<IResult>(Results.BadRequest()),
                    async planned =>
                    {
                        if (!await planned.HasKnownLocale(configurationFile.Name, locales, cancellationToken))
                        {
                            return Results.BadRequest();
                        }

                        await configurationFile.SetJsonNodesAsync(planned, cancellationToken);

                        logger.LogInformation(
                            "Settings of {File} changed: {Paths}",
                            () => [configurationFile.Name, string.Join(", ", planned.Keys)]);

                        return Results.NoContent();
                    });
            });
    }

    private static async Task<IResult> UpdateAppSettings(
        FileInfo target,
        SettingsUpdateRequest request,
        ILogger<SettingsEndpoints> logger,
        CancellationToken cancellationToken)
    {
        if (request.Values is null
            || request.Values.Count != 0
            || request.Logging is null
            || request.OpenTelemetry is null)
        {
            return Results.BadRequest();
        }

        var logging = request.Logging.ToConfiguration();
        var openTelemetry = request.OpenTelemetry.ToNormalizedRequest();

        return await logging.Match(
            () => Task.FromResult<IResult>(Results.BadRequest()),
            async minimumLevel => await openTelemetry.Match(
                () => Task.FromResult<IResult>(Results.BadRequest()),
                async configuration =>
                {
                    await target.WriteAppSettingsAsync(minimumLevel, configuration, cancellationToken);

                    logger.LogInformation("App settings of {File} changed", () => [target.Name]);

                    return Results.NoContent();
                }));
    }

    public static Task<IResult> Generate(
        string file,
        SettingsActionRequest request,
        ConfigurationFileCatalog catalog,
        IOptions<SecurityConfiguration> security,
        ILogger<SettingsEndpoints> logger,
        CancellationToken cancellationToken) =>
        Modify(
            catalog.Find(file),
            security,
            target => target.Name
                .FindAction(request.Path, SettingsActionKind.Generate)
                .Match(
                    () => Task.FromResult<IResult>(Results.BadRequest()),
                    action => GenerateSecret(target, action, logger, cancellationToken)));

    public static Task<IResult> Rotate(
        string file,
        SettingsActionRequest request,
        ConfigurationFileCatalog catalog,
        IOptions<SecurityConfiguration> security,
        MagicLinkSecretRotator rotator,
        ILogger<SettingsEndpoints> logger,
        CancellationToken cancellationToken) =>
        Modify(
            catalog.Find(file),
            security,
            target => target.Name
                .FindAction(request.Path, SettingsActionKind.Rotate)
                .Match(
                    () => Task.FromResult<IResult>(Results.BadRequest()),
                    async action =>
                    {
                        if (!await rotator.RotateNowAsync(cancellationToken))
                        {
                            return Results.NotFound();
                        }

                        logger.LogInformation("Setting {Path} of {File} rotated", () => [action.Path, target.Name]);

                        return Results.Json(new SettingsActionResponse(action.Path, null));
                    }));

    public static Task<IResult> GetEmail(ConfigurationFileCatalog catalog) =>
        Task.FromResult(
            catalog.FindWithEditor(ConfigurationFileCatalog.EmailFile, ConfigurationFileCatalog.EmailEditor).Match(
                () => Results.NotFound(),
                file => Results.Json(file.ReadEmailConfiguration().ToSettings())));

    public static Task<IResult> UpdateEmail(
        EmailSettingsRequest request,
        ConfigurationFileCatalog catalog,
        IOptions<SecurityConfiguration> security,
        ILogger<SettingsEndpoints> logger,
        CancellationToken cancellationToken) =>
        Modify(
            catalog.FindWithEditor(ConfigurationFileCatalog.EmailFile, ConfigurationFileCatalog.EmailEditor),
            security,
            async target =>
            {
                var configuration = target.ReadEmailConfiguration().Apply(request);

                return await configuration.Match(
                    () => Task.FromResult<IResult>(Results.BadRequest()),
                    async applied =>
                    {
                        await target.WriteEmailConfigurationAsync(applied, cancellationToken);

                        logger.LogInformation("Email provider of {File} set to {Provider}", () => [target.Name, applied.Provider]);

                        return Results.NoContent();
                    });
            });

    public static async Task<IResult> GetLogging(
        string file,
        ConfigurationFileCatalog catalog,
        CancellationToken cancellationToken) =>
        await catalog.FindWithEditor(file, ConfigurationFileCatalog.LoggingEditor).Match(
            () => Task.FromResult<IResult>(Results.NotFound()),
            async target => Results.Json((await target.ReadMinimumLevelAsync(cancellationToken)).ToSettings()));

    public static async Task<IResult> GetOpenTelemetry(
        string file,
        ConfigurationFileCatalog catalog,
        CancellationToken cancellationToken) =>
        await catalog.FindWithEditor(file, ConfigurationFileCatalog.LoggingEditor).Match(
            () => Task.FromResult<IResult>(Results.NotFound()),
            async target => Results.Json(await target.ReadOpenTelemetrySettingsAsync(cancellationToken)));

    private static async Task<IResult> GenerateSecret(
        FileInfo target,
        SettingsAction action,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var root = await target.ReadJsonAsync(cancellationToken);

        if (root.FindValue(action.Path) is null && !action.CreatesMissing)
        {
            return Results.NotFound();
        }

        var generated = action.Generate!();

        await target.SetJsonValuesAsync(
            new Dictionary<string, string?> { [action.Path] = generated.Value },
            cancellationToken);

        logger.LogInformation("Setting {Path} of {File} generated", () => [action.Path, target.Name]);

        return Results.Json(
            new SettingsActionResponse(action.Path, generated.OtpUri.Match<string?>(() => null, uri => uri)));
    }

    private static Task<IResult> Modify(
        Option<FileInfo> target,
        IOptions<SecurityConfiguration> security,
        Func<FileInfo, Task<IResult>> operation) =>
        security.Value.ReadOnlyConfiguration
            ? Task.FromResult<IResult>(Results.Json(ConfigurationForbiddenResponse.ConfigurationReadOnly, statusCode: StatusCodes.Status403Forbidden))
            : target.Match(
                () => Task.FromResult<IResult>(Results.NotFound()),
                operation);
}
