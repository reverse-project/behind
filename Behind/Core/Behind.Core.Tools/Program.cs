using System.CommandLine;
using OtpNet;

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

if (args.Length == 0)
{
    var quitCommand = new Command("quit");
    quitCommand.SetAction(_ => Environment.Exit(0));
    rootCommand.Add(quitCommand);
}

do
{
    var parameters = args.Length > 0 ? args : Console.ReadLine()?.Split(' ') ?? [];
    var result = rootCommand.Parse(parameters);
    if (result.Errors.Count > 0)
    {
        foreach (var error in result.Errors)
        {
            Console.WriteLine(error);
        }
    }

    result.Invoke();
} while (args.Length == 0);