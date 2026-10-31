using System.Text.Json;
using System.Text.Json.Nodes;

namespace Behind.Common.Configuration;

public static class JsonConfigurationFileExtensions
{
    private const char PathSeparator = ':';

    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    private static readonly JsonDocumentOptions ReadOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    extension(FileInfo file)
    {
        public async Task SetJsonValuesAsync(
            IReadOnlyDictionary<string, string?> values,
            CancellationToken cancellationToken = default)
        {
            await WriteGate.WaitAsync(cancellationToken);

            try
            {
                var root = await file.ReadRootAsync(cancellationToken);

                foreach (var (path, value) in values)
                {
                    root.SetValue(path.Split(PathSeparator), value);
                }

                await file.ReplaceContentAsync(root.ToJsonString(WriteOptions), cancellationToken);
            }
            finally
            {
                WriteGate.Release();
            }
        }

        private async Task<JsonObject> ReadRootAsync(CancellationToken cancellationToken)
        {
            await using var stream = file.OpenRead();
            var node = await JsonNode.ParseAsync(stream, documentOptions: ReadOptions, cancellationToken: cancellationToken);

            return node as JsonObject ?? throw new InvalidDataException($"'{file.Name}' must contain a JSON object.");
        }

        private async Task ReplaceContentAsync(string content, CancellationToken cancellationToken)
        {
            var temporary = $"{file.FullName}.{Guid.NewGuid():N}.tmp";

            try
            {
                await File.WriteAllTextAsync(temporary, content, cancellationToken);
                File.Move(temporary, file.FullName, overwrite: true);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }

    extension(JsonObject root)
    {
        private void SetValue(string[] path, string? value)
        {
            var parent = path[..^1].Aggregate(
                root,
                (current, segment) => current[segment] as JsonObject ?? current.ReplaceWithObject(segment));

            if (value is null)
            {
                parent.Remove(path[^1]);
                return;
            }

            parent[path[^1]] = value;
        }

        private JsonObject ReplaceWithObject(string name)
        {
            var created = new JsonObject();
            root[name] = created;
            return created;
        }
    }
}
