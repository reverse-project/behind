using System.CommandLine;

namespace Behind.Core.Tools;

public static class StartupCommandExtensions
{
    extension(RootCommand root)
    {
        public RootCommand AddStartupCommands()
        {
            var dataDirectory = new Option<string>("--data-directory", "-d")
            {
                Description = "Data directory that holds the certificate, the data protection keys and the database",
                DefaultValueFactory = _ => "data"
            };
            var configDirectory = new Option<string>("--config-directory", "-c")
            {
                Description = "Directory where security.json, database.json, backend.json and frontend.json are generated",
                DefaultValueFactory = _ => "."
            };
            var issuer = new Option<string>("--issuer")
            {
                Description = "JWT issuer",
                DefaultValueFactory = _ => StartupSetupRequest.DefaultIssuer
            };
            var audience = new Option<string>("--audience")
            {
                Description = "JWT audience",
                DefaultValueFactory = _ => StartupSetupRequest.DefaultAudience
            };
            var emailConfirmation = new Option<bool>("--email-confirmation")
            {
                Description = "Require email confirmation (needs a configured email sender)"
            };
            var prompt = new Option<bool>("--prompt", "-p")
            {
                Description = "Prompt for the certificate password instead of generating a random one"
            };
            var force = new Option<bool>("--force", "-f")
            {
                Description = "Overwrite the existing certificate and configuration files"
            };
            var skipExisting = new Option<bool>("--skip-existing", "-s")
            {
                Description = "Keep the configuration files that already exist, so the command can run on every container start"
            };

            var create = new Command(
                "create",
                "Generate the startup configuration files with all their secrets and the data protection certificate")
            {
                dataDirectory, configDirectory, issuer, audience, emailConfirmation, prompt, force, skipExisting
            };
            create.SetAction(arguments =>
            {
                new StartupSetupRequest(
                    arguments.GetRequiredValue(dataDirectory),
                    arguments.GetRequiredValue(configDirectory),
                    arguments.GetRequiredValue(issuer),
                    arguments.GetRequiredValue(audience),
                    arguments.GetValue(emailConfirmation),
                    ConsolePasswordReader.Resolve(arguments.GetValue(prompt)),
                    arguments.GetValue(force),
                    arguments.GetValue(skipExisting)
                ).Run(Console.Out);
            });

            root.Add(new Command("startup", "Startup configuration related tools") { create });

            return root;
        }
    }
}
