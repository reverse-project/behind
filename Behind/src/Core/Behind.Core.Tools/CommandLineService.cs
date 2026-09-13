using System.CommandLine;
using Microsoft.Extensions.Hosting;
using OtpNet;

namespace Behind.Core.Tools;

public sealed class CommandLineService(IHost host) : BackgroundService
{
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
            var size = arguments.GetValue<ushort?>("--size") ?? 20;

            switch (arguments.GetValue<string>("--type"))
            {
                case "totp":
                    var randomKey = KeyGeneration.GenerateRandomKey(size);
                    Console.WriteLine("To security.json as TotpSecretKey: {0}", Base32Encoding.ToString(randomKey));
                    Console.WriteLine("To your authenticator: {0}",
                        new OtpUri(OtpType.Totp, randomKey, "admin", "Behind System", OtpHashMode.Sha256));
                    break;
            }
        });
        secretCommand.Add(generateSecretCommand);
        rootCommand.Add(secretCommand);

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
}