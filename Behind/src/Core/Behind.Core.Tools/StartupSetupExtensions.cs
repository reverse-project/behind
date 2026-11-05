using System.Text;
using Behind.Common.Security;
using Behind.Core.Common.Configuration;
using static Behind.Common.FunctionalExtensions;
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
            new(
                request.ConfigPath(StartupSetupRequest.DatabaseFileName),
                [request.ConfigPath(StartupSetupRequest.DatabaseFileName)],
                request.RenderDatabase),
            new(
                request.ConfigPath(StartupSetupRequest.BackendFileName),
                [request.ConfigPath(StartupSetupRequest.BackendFileName)],
                request.RenderBackend),
            new(
                request.ConfigPath(StartupSetupRequest.FrontendFileName),
                [request.ConfigPath(StartupSetupRequest.FrontendFileName)],
                request.RenderFrontend)
        ];

        public IReadOnlyList<StartupPlanStep> Plan =>
            [..request.Artifacts.Select(artifact =>
                new StartupPlanStep(artifact, artifact.PlanAction(request.Force, request.SkipExisting)))];

        public void Run(TextWriter output)
        {
            var plan = request.Plan;
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

        private StartupRendering RenderSecurity()
        {
            var secretKey = TotpValue.Generate();
            var developerKey = TotpValue.Generate();
            var dataProtection = new DataProtectionSetupRequest(
                request.DataPath(StartupSetupRequest.CertificateFileName),
                request.DataPath(StartupSetupRequest.KeysDirectoryName),
                request.Password,
                None,
                false).CreateCertificate();

            Directory.CreateDirectory(dataProtection.KeysPath);

            return new StartupRendering(
                new StartupSecurityFile(new StartupSecuritySection(
                    request.EmailConfirmation,
                    dataProtection,
                    secretKey,
                    new JwtSettings(request.Issuer, request.Audience, JwtSettings.GenerateSigningKey()),
                    new ElevationSettings(true, Some<SecretKey>(developerKey)),
                    new StartupMagicLinkSettings(Uuid.CreateVersion7(DateTimeOffset.UtcNow))
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
                        Some(new Key(StartupSetupRequest.ConnectionStringName)))
                ).ToConfigurationJson(),
                []);

        private StartupRendering RenderBackend() =>
            new(
                new StartupBackendFile(new StartupBackendSection(Path.GetFullPath(request.DataDirectory)))
                    .ToConfigurationJson(),
                []);

        private StartupRendering RenderFrontend() =>
            new(
                new StartupFrontendFile(new StartupFrontendSection(
                    new StartupSiteSection(
                        StartupSetupRequest.DefaultTitle,
                        string.Empty,
                        StartupSetupRequest.DefaultLocale),
                    new StartupSitesSection(string.Empty, SitesConfiguration.DefaultMaxImageBytes),
                    new StartupCarouselsSection(
                        CarouselsConfiguration.DefaultMaxSlides,
                        CarouselsConfiguration.DefaultMinIntervalSeconds,
                        CarouselsConfiguration.DefaultMaxIntervalSeconds,
                        CarouselsConfiguration.DefaultIntervalSeconds,
                        CarouselsConfiguration.DefaultMaxCaptionLength,
                        CarouselsConfiguration.DefaultMaxImageBytes)))
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
                    step.Artifact.OwnedPaths.ToList().ForEach(File.Delete);
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

            step.Artifact.ConfigurationPath.WriteNewFile(Encoding.UTF8.GetBytes(rendering.Content));
            output.WriteLine("Created: {0}", step.Artifact.ConfigurationPath);
            rendering.Messages.ToList().ForEach(message => output.WriteLine(message));
        }
    }
}
