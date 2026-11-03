using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Settings;

public sealed record ConfigurationFileCatalog(DirectoryInfo Root, IReadOnlyList<string> Names)
{
    public const string EmailEditor = "email";
    public const string EmailFile = "email.json";
    public const string LoggingEditor = "logging";
    public const string AppSettingsFile = "appsettings.json";

    public static readonly IReadOnlyList<string> DefaultNames =
    [
        AppSettingsFile,
        "backend.json",
        "database.json",
        EmailFile,
        "frontend.json",
        "security.json"
    ];

    private static readonly IReadOnlyDictionary<string, string> Editors =
        new Dictionary<string, string>
        {
            [EmailFile] = EmailEditor,
            [AppSettingsFile] = LoggingEditor
        };

    public static ConfigurationFileCatalog For(string root) => new(new DirectoryInfo(root), DefaultNames);

    public IReadOnlyList<FileInfo> Files =>
    [
        ..Names
            .Select(name => new FileInfo(Path.Combine(Root.FullName, name)))
            .Where(file => file.Exists)
    ];

    public Option<string> EditorOf(FileInfo file) =>
        Editors.TryGetValue(file.Name, out var editor) ? Some(editor) : None;

    public Option<FileInfo> Find(string name) =>
        Files.Find(file => IsNamed(file, name) && EditorOf(file) == None);

    public Option<FileInfo> FindWithEditor(string name, string editor) =>
        Files.Find(file => IsNamed(file, name) && EditorOf(file) == Some(editor));

    private static bool IsNamed(FileInfo file, string name) =>
        string.Equals(file.Name, name, StringComparison.OrdinalIgnoreCase);
}
