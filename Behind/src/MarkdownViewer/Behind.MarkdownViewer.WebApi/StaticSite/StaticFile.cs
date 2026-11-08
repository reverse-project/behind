namespace Behind.MarkdownViewer.WebApi.StaticSite;

public sealed record StaticFile(string Path, Func<string, CancellationToken, Task> WriteTo)
{
    public static StaticFile Text(string path, string content) =>
        new(path, (target, cancellationToken) => File.WriteAllTextAsync(target, content, cancellationToken));

    public static StaticFile Copy(string path, FileInfo source) =>
        new(path, (target, _) =>
        {
            source.CopyTo(target, true);

            return Task.CompletedTask;
        });
}
