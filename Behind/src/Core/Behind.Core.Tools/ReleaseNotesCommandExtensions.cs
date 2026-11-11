using System.CommandLine;
using System.Text.Json;

namespace Behind.Core.Tools;

public static class ReleaseNotesCommandExtensions
{
    extension(RootCommand root)
    {
        public RootCommand AddReleaseNotesCommands()
        {
            var version = new Option<string?>("--version")
            {
                Description = "Changelog version to render (defaults to the first, latest release)"
            };
            var changelog = new Option<string>("--changelog")
            {
                Description = "Path to the changelog JSON file",
                DefaultValueFactory = _ => FindDefaultChangelogPath()
            };
            var command = new Command("release-notes", "Generate Markdown release notes from the changelog")
            {
                version,
                changelog
            };
            command.SetAction(arguments =>
            {
                try
                {
                    string json = File.ReadAllText(arguments.GetRequiredValue(changelog));
                    Console.Out.Write(ReleaseNotesGenerator.Generate(json, arguments.GetValue(version)));
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
                {
                    Console.Error.WriteLine($"Error: {error.Message}");
                    Environment.ExitCode = 1;
                }
            });

            root.Add(command);
            return root;
        }
    }

    private static string FindDefaultChangelogPath()
    {
        const string relativePath = "src/Core/Behind.Core.WebApi/Frontend/changelog.json";
        for (DirectoryInfo? directory = new(Environment.CurrentDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            string[] candidates =
            [
                Path.Combine(directory.FullName, relativePath),
                Path.Combine(directory.FullName, "Behind", relativePath)
            ];
            string? changelog = candidates.FirstOrDefault(File.Exists);
            if (changelog is not null)
            {
                return changelog;
            }
        }

        return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, relativePath));
    }
}
