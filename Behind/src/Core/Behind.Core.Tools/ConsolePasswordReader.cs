using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Behind.Common.Security;

namespace Behind.Core.Tools;

[ExcludeFromCodeCoverage]
public static class ConsolePasswordReader
{
    public static string Resolve(bool prompt) =>
        prompt ? ReadConfirmed() : RandomNumberGenerator.CreatePassword();

    public static string ReadConfirmed()
    {
        string password = Read("Certificate password: ");

        return password.Length == 0
            ? throw new InvalidOperationException("Password cannot be empty.")
            : password == Read("Confirm password: ")
                ? password
                : throw new InvalidOperationException("Passwords do not match.");
    }

    private static string Read(string prompt)
    {
        Console.Error.Write(prompt);

        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var password = new StringBuilder();

        for (ConsoleKeyInfo key = Console.ReadKey(true); key.Key != ConsoleKey.Enter; key = Console.ReadKey(true))
        {
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }

        Console.Error.WriteLine();

        return password.ToString();
    }
}
