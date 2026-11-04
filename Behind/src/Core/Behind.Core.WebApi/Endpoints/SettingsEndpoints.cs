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
        CancellationToken cancellationToken) =>
        Modify(
            catalog.Find(file),
            security,
            async target =>
            {
                var changes = (await target.ReadJsonAsync(cancellationToken)).PlanChanges(target.Name, request.Values);

                return await changes.Match(
                    () => Task.FromResult<IResult>(Results.BadRequest()),
                    async planned =>
                    {
                        if (!await planned.HasKnownLocale(target.Name, locales, cancellationToken))
                        {
                            return Results.BadRequest();
                        }

                        await target.SetJsonNodesAsync(planned, cancellationToken);

                        logger.LogInformation(
                            "Settings of {File} changed: {Paths}",
                            () => [target.Name, string.Join(", ", planned.Keys)]);

                        return Results.NoContent();
                    });
            });

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

    public static Task<IResult> UpdateLogging(
        string file,
        LoggingSettingsRequest request,
        ConfigurationFileCatalog catalog,
        IOptions<SecurityConfiguration> security,
        ILogger<SettingsEndpoints> logger,
        CancellationToken cancellationToken) =>
        Modify(
            catalog.FindWithEditor(file, ConfigurationFileCatalog.LoggingEditor),
            security,
            target => request.ToConfiguration().Match(
                () => Task.FromResult<IResult>(Results.BadRequest()),
                async configuration =>
                {
                    await target.WriteMinimumLevelAsync(configuration, cancellationToken);

                    logger.LogInformation("Log levels of {File} changed", () => [target.Name]);

                    return Results.NoContent();
                }));

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
