using System.CommandLine;
using System.Security.Cryptography;
using Behind.Common.Security;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Tools;

public static class DataProtectionCommandExtensions
{
    extension(RootCommand root)
    {
        public RootCommand AddDataProtectionCommands()
        {
            var certificate = new Option<string>("--certificate", "-c")
            {
                Description = "Certificate (.pfx) path",
                DefaultValueFactory = _ => "dataprotection.pfx"
            };
            var keys = new Option<string>("--keys-path", "-k")
            {
                Description = "Directory where the data protection keys are persisted",
                DefaultValueFactory = _ => "DataProtection-Keys"
            };
            var output = new Option<string>("--output", "-o")
            {
                Description = "Security configuration file to generate",
                DefaultValueFactory = _ => "security.json"
            };
            var prompt = new Option<bool>("--prompt", "-p")
            {
                Description = "Prompt for the certificate password instead of generating a random one"
            };
            var force = new Option<bool>("--force", "-f")
            {
                Description = "Overwrite existing certificate and output files"
            };

            var create = new Command("create", "Create the data protection certificate and its security.json")
            {
                certificate, keys, output, prompt, force
            };
            create.SetAction(arguments =>
            {
                new DataProtectionSetupRequest(
                    arguments.GetRequiredValue(certificate),
                    arguments.GetRequiredValue(keys),
                    arguments.GetValue(prompt)
                        ? ConsolePasswordReader.ReadConfirmed()
                        : RandomNumberGenerator.CreatePassword(),
                    Some(arguments.GetRequiredValue(output)),
                    arguments.GetValue(force)
                ).Run(Console.Out);
            });

            var dataProtection = new Command("dataprotection", "Data protection related tools") { create };
            root.Add(dataProtection);

            return root;
        }
    }
}
