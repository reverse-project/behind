using System.Text;
using System.Text.Json.Nodes;
using Behind.Common.Configuration;
using Behind.Common.Logging;
using Behind.Common.Security;
using Behind.Core.Common.Configuration;
using static Behind.Common.FunctionalExtensions;
using OpenTelemetry.Exporter;
using TotpValue = Behind.Core.Common.Configuration.Totp;

namespace Behind.Core.Tools;

public static class StartupSetupExtensions
{
    extension(StartupArtifact artifact)
    {
        public StartupArtifactAction PlanAction(bool force, bool skipExisting) =>
            artifact.OwnedPaths.Count(File.Exists) switch
            {
                0 => StartupArtifactAction.Generate,
                _ when force => StartupArtifactAction.Replace,
                var present when skipExisting && present == artifact.OwnedPaths.Count =>
                    StartupArtifactAction.Skip,
                _ => StartupArtifactAction.Conflict
            };
    }

    extension(StartupSetupRequest request)
    {
        public string DataPath(string name) =>
            Path.GetFullPath(Path.Combine(request.DataDirectory, name));

        public string ConfigPath(string name) =>
            Path.GetFullPath(Path.Combine(request.ConfigDirectory, name));

        public IReadOnlyList<StartupArtifact> Artifacts =>
        [
            new(
                request.ConfigPath(StartupSetupRequest.SecurityFileName),
                [
                    request.ConfigPath(StartupSetupRequest.SecurityFileName),
                    request.DataPath(StartupSetupRequest.CertificateFileName)
                ],
                request.RenderSecurity),
            request.ConfigurationArtifact(StartupSetupRequest.DatabaseFileName, request.RenderDatabase),
            request.ConfigurationArtifact(StartupSetupRequest.BackendFileName, request.RenderBackend),
            request.ConfigurationArtifact(StartupSetupRequest.CachingFileName, RenderCaching),
            request.ConfigurationArtifact(StartupSetupRequest.FrontendFileName, request.RenderFrontend),
            request.ConfigurationArtifact(StartupSetupRequest.EmailFileName, RenderEmail),
            request.ConfigurationArtifact(StartupSetupRequest.AppSettingsFileName, RenderAppSettings),
            request.ConfigurationArtifact(StartupSetupRequest.ForumFileName, RenderForum),
            request.ConfigurationArtifact(StartupSetupRequest.MarkdownViewerFileName, request.RenderMarkdownViewer)
        ];

        public IReadOnlyList<StartupPlanStep> Plan =>
            [..request.Artifacts.Select(artifact =>
                new StartupPlanStep(artifact, artifact.PlanAction(request.Force, request.SkipExisting)))];

        public void Run(TextWriter output)
        {
            var plan = request.Plan;
            var directories = plan.SelectMany(step => step.Artifact.OwnedPaths)
                .Where(Directory.Exists).ToList();
            if (directories.Count > 0)
            {
                throw new IOException($"Expected files but found directories: {string.Join(", ", directories)}.");
            }

            var conflicts = plan.Where(step => step.Action == StartupArtifactAction.Conflict).ToList();

            if (conflicts.Count > 0)
            {
                throw new IOException(
                    $"Already exists: {string.Join(", ", conflicts.SelectMany(step => step.Artifact.OwnedPaths.Where(File.Exists)))}. "
                    + "Use --force to overwrite or --skip-existing to keep them.");
            }

            foreach (var step in plan)
            {
                step.Execute(output);
            }
        }

        private StartupArtifact ConfigurationArtifact(string fileName, Func<StartupRendering> render) =>
            new(request.ConfigPath(fileName), [request.ConfigPath(fileName)], render);

        private StartupRendering RenderSecurity()
        {
            var secretKey = TotpValue.Generate();
            var developerKey = TotpValue.Generate();
            var dataProtection = new DataProtectionSetupRequest(
                request.DataPath(StartupSetupRequest.CertificateFileName),
                request.DataPath(StartupSetupRequest.KeysDirectoryName),
                request.Password,
                None,
                request.Force).CreateCertificate();

            Directory.CreateDirectory(dataProtection.KeysPath);

            return new StartupRendering(
                new StartupSecurityFile(new StartupSecuritySection(
                    false,
                    request.EmailConfirmation,
                    false,
                    dataProtection,
                    secretKey,
                    new JwtSettings(request.Issuer, request.Audience, JwtSettings.GenerateSigningKey()),
                    new ElevationSettings(true, Some<SecretKey>(developerKey)),
                    new StartupMagicLinkSettings(
                        Uuid.CreateVersion7(DateTimeOffset.UtcNow),
                        MagicLinkSettings.DefaultRotationInterval,
                        MagicLinkSettings.DefaultRotationOverlap,
                        MagicLinkSettings.DefaultLinkLifetime,
                        MagicLinkSettings.DefaultRequestCooldown,
                        MagicLinkSettings.DefaultRequestLimit,
                        MagicLinkSettings.DefaultRequestWindow,
                        MagicLinkSettings.DefaultRedeemLimit,
                        MagicLinkSettings.DefaultRedeemWindow),
                    new OidcConfiguration()
                )).ToConfigurationJson(),
                [
                    $"Certificate: {dataProtection.CertificatePath}",
                    $"Authenticator URI of the secret key ({StartupSetupRequest.AdminAccount}): "
                    + secretKey.ToOtpUri(StartupSetupRequest.AdminAccount, TotpValue.Issuer),
                    $"Authenticator URI of the developer elevation key ({StartupSetupRequest.DeveloperAccount}): "
                    + developerKey.ToOtpUri(StartupSetupRequest.DeveloperAccount, TotpValue.Issuer)
                ]);
        }

        private StartupRendering RenderDatabase() =>
            new(
                new StartupDatabaseFile(
                    new Dictionary<string, string>
                    {
                        [StartupSetupRequest.ConnectionStringName] =
                            $"Data Source={request.DataPath(StartupSetupRequest.DatabaseFileNameInData)}"
                    },
                    new DatabaseConfiguration(
                        Some<Provider>(new Sqlite()),
                        Some(new DatabaseKey(StartupSetupRequest.ConnectionStringName)))
                ).ToConfigurationJson(),
                []);

        private StartupRendering RenderBackend() =>
            new(
                new StartupBackendFile(new StartupBackendSection(
                        Path.GetFullPath(request.DataDirectory),
                        new ContentRestrictionsConfiguration(),
                        new DownloadsConfiguration(),
                        new CommentsConfiguration(),
                        new AccountsConfiguration(),
                        new SchedulerConfiguration()))
                    .ToConfigurationJson(),
                []);

        private StartupRendering RenderFrontend() =>
            new(
                new StartupFrontendFile(new StartupFrontendSection(
                        request.FrontendUrl,
                        new StartupSiteSection(
                            StartupSetupRequest.DefaultTitle,
                            string.Empty,
                            StartupSetupRequest.DefaultLocale),
                        new SitesConfiguration { IndexSlug = string.Empty },
                        new GalleriesConfiguration(),
                        new CarouselsConfiguration(),
                        new StartupStaticFilesSection()))
                    .ToConfigurationJson(),
                []);

        private StartupRendering RenderMarkdownViewer() =>
            new(
                new StartupMarkdownViewerFile(new StartupMarkdownViewerSection(
                        request.DataPath(StartupSetupRequest.DocsDirectoryName),
                        request.DataPath(StartupSetupRequest.DocsSiteDirectoryName),
                        StartupMarkdownViewerSection.DefaultTitle,
                        StartupMarkdownViewerSection.DefaultDocumentName))
                    .ToConfigurationJson(),
                []);
    }

    private static StartupRendering RenderEmail() =>
        new(new StartupEmailFile(new LoggerEmailConfiguration()).ToConfigurationJson(), []);

    private static StartupRendering RenderCaching() =>
        new(new StartupCachingFile(
            new Dictionary<string, string> { ["CachingConnection"] = string.Empty },
            new CachingConfiguration { Key = Some(new CachingKey("CachingConnection")) })
            .ToConfigurationJson(), []);

    private static StartupRendering RenderForum() =>
        new(new StartupForumFile(new StartupForumSection()).ToConfigurationJson(), []);

    private const string AspNetCoreInstrumentation = nameof(OpenTelemetryConfiguration.Instrumentation.AspNetCoreInstrumentation);
    private const string HttpClientInstrumentation = nameof(OpenTelemetryConfiguration.Instrumentation.HttpClientInstrumentation);
    private const string RuntimeInstrumentation = nameof(OpenTelemetryConfiguration.Instrumentation.RuntimeInstrumentation);
    private const string OtlpExporter = nameof(OpenTelemetryConfiguration.Instrumentation.OtlpExporter);

    private static StartupRendering RenderAppSettings()
    {
        using var defaults = typeof(SerilogExtensions).Assembly
                                 .GetManifestResourceStream(SerilogExtensions.DefaultsResourceName)
                             ?? throw new InvalidOperationException(
                                 $"Embedded resource '{SerilogExtensions.DefaultsResourceName}' not found.");

        return new(
            new StartupAppSettingsFile(
                    JsonNode.Parse(defaults)?[StartupSetupRequest.SerilogSectionName]?.DeepClone(),
                    new StartupOpenTelemetrySection(
                        StartupSetupRequest.DefaultServiceName,
                        false,
                        new StartupOtlpExporterSection(
                            false,
                            StartupSetupRequest.DefaultOtlpEndpoint,
                            nameof(OtlpExportProtocol.Grpc),
                            [AspNetCoreInstrumentation, HttpClientInstrumentation, OtlpExporter]),
                        new StartupOtlpExporterSection(
                            false,
                            StartupSetupRequest.DefaultOtlpEndpoint,
                            nameof(OtlpExportProtocol.Grpc),
                            [AspNetCoreInstrumentation, HttpClientInstrumentation, RuntimeInstrumentation, OtlpExporter]),
                        new StartupOtlpLoggingSection(
                            false,
                            StartupSetupRequest.DefaultOtlpEndpoint,
                            nameof(OtlpExportProtocol.Grpc),
                            [OtlpExporter],
                            true,
                            true,
                            true)),
                    StartupSetupRequest.DefaultAllowedHosts)
                .ToConfigurationJson(),
            []);
    }

    extension(StartupPlanStep step)
    {
        public void Execute(TextWriter output)
        {
            switch (step.Action)
            {
                case StartupArtifactAction.Skip:
                    output.WriteLine("Skipped (already exists): {0}", step.Artifact.ConfigurationPath);
                    break;
                case StartupArtifactAction.Replace:
                    step.Write(output);
                    break;
                default:
                    step.Write(output);
                    break;
            }
        }

        private void Write(TextWriter output)
        {
            var rendering = step.Artifact.Render();

            ConfigurationSchema.Validate(JsonNode.Parse(rendering.Content));
            step.Artifact.ConfigurationPath.WriteFileDirect(
                Encoding.UTF8.GetBytes(rendering.Content),
                step.Action == StartupArtifactAction.Replace);
            output.WriteLine("Created: {0}", step.Artifact.ConfigurationPath);
            rendering.Messages.ToList().ForEach(message => output.WriteLine(message));
        }
    }
}
