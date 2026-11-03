using System.CommandLine;
using Behind.Core.Common.Configuration;
using Behind.Core.Common.Converters;
using Microsoft.Extensions.Hosting;
using TotpValue = Behind.Core.Common.Configuration.Totp;

namespace Behind.Core.Tools;

public sealed class CommandLineService(IHost host) : BackgroundService
{
    private const string DefaultAccount = "admin";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rootCommand = new RootCommand("Behind Core Command Line Tools");
        var secretCommand = new Command("secret", "Secret related tools");
        var generateSecretCommand = new Command("generate", "Generate secret")
        {
            new Option<string>("--type", "-t"),
            new Option<ushort?>("--size", "-s"),
        };
        generateSecretCommand.SetAction(arguments =>
        {
            switch (arguments.GetValue<string>("--type"))
            {
                case "totp":
                    PrintTotp(TotpValue.Generate(arguments.GetValue<ushort?>("--size")), DefaultAccount);
                    break;
            }
        });
        var uriCommand = new Command("uri", "Print the authenticator URI of an existing totp secret key value")
        {
            new Option<string>("--value", "-v") { Required = true },
            new Option<string>("--account", "-a") { DefaultValueFactory = _ => DefaultAccount },
        };
        uriCommand.SetAction(arguments =>
        {
            var value = new SecretKeyTypeConverter().ConvertFrom(arguments.GetValue<string>("--value")!);

            if (value is TotpValue totp)
            {
                PrintTotp(totp, arguments.GetValue<string>("--account")!);
                return;
            }

            Console.WriteLine("The value is not a totp secret key.");
        });
        secretCommand.Add(generateSecretCommand);
        secretCommand.Add(uriCommand);
        rootCommand.Add(secretCommand);
        rootCommand.AddDataProtectionCommands();

        var parameters = Environment.GetCommandLineArgs()[1..];
        
        var result = rootCommand.Parse(parameters.Length == 0 ? ["--help"] : parameters);
        if (result.Errors.Count > 0)
        {
            foreach (var error in result.Errors)
            {
                Console.WriteLine(error.Message);
            }
        }

        await result.InvokeAsync(null, stoppingToken);

        await host.StopAsync(stoppingToken);
    }

    private static void PrintTotp(TotpValue totp, string account)
    {
        Console.WriteLine("To security.json as a secret key value: {0}", totp.ToConfigurationValue());
        Console.WriteLine("To your authenticator (paste the whole URI): {0}", totp.ToOtpUri(account, TotpValue.Issuer));
    }
}
