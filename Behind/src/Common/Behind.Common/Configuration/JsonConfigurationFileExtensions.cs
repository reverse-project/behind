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
        public Task SetJsonValuesAsync(
            IReadOnlyDictionary<string, string?> values,
            CancellationToken cancellationToken = default) =>
            file.SetJsonNodesAsync(
                values.ToDictionary(pair => pair.Key, pair => pair.Value is null ? null : JsonValue.Create(pair.Value) as JsonNode),
                cancellationToken);

        public async Task SetJsonNodesAsync(
            IReadOnlyDictionary<string, JsonNode?> values,
            CancellationToken cancellationToken = default)
        {
            await WriteGate.WaitAsync(cancellationToken);

            try
            {
                var root = await file.ReadJsonAsync(cancellationToken);

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

        public async Task<JsonObject> ReadJsonAsync(CancellationToken cancellationToken = default)
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
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
            {
                await File.WriteAllTextAsync(file.FullName, content, cancellationToken);
            }
            finally
            {
                File.Delete(temporary);
            }
        }
    }

    extension(JsonObject root)
    {
        public JsonValue? FindValue(string path) =>
            path.Split(PathSeparator).Aggregate((JsonNode?)root, (current, segment) => current?.Child(segment)) as JsonValue;

        public IReadOnlyList<JsonConfigurationField> Fields() => [..root.Leaves(string.Empty)];

        private void SetValue(string[] path, JsonNode? value)
        {
            var parent = path[..^1].Aggregate(
                (JsonNode)root,
                (current, segment) => current.Child(segment) is { } child and (JsonObject or JsonArray)
                    ? child
                    : ((JsonObject)current).ReplaceWithObject(segment));

            parent.SetChild(path[^1], value);
        }
    }

    extension(JsonNode node)
    {
        private JsonNode? Child(string segment) =>
            node switch
            {
                JsonObject container => container[segment],
                JsonArray container when container.TryIndex(segment, out var index) => container[index],
                _ => null
            };

        private void SetChild(string segment, JsonNode? value)
        {
            switch (node)
            {
                case JsonObject container when value is null:
                    container.Remove(segment);
                    break;
                case JsonObject container:
                    container[segment] = value;
                    break;
                case JsonArray container when container.TryIndex(segment, out var index):
                    container[index] = value;
                    break;
            }
        }

        private IEnumerable<JsonConfigurationField> Leaves(string prefix) =>
            node switch
            {
                JsonObject container => container.SelectMany(pair => pair.Value?.Leaves(prefix.Join(pair.Key)) ?? []),
                JsonArray container => container.SelectMany((item, index) => item?.Leaves(prefix.Join(index.ToString())) ?? []),
                JsonValue value => [new JsonConfigurationField(prefix, value)],
                _ => []
            };
    }

    extension(JsonArray array)
    {
        private bool TryIndex(string segment, out int index) =>
            int.TryParse(segment, out index) && index >= 0 && index < array.Count;
    }

    extension(JsonObject container)
    {
        private JsonObject ReplaceWithObject(string name)
        {
            var created = new JsonObject();
            container[name] = created;
            return created;
        }
    }

    extension(string prefix)
    {
        private string Join(string segment) => prefix.Length == 0 ? segment : $"{prefix}{PathSeparator}{segment}";
    }
}

public sealed record JsonConfigurationField(string Path, JsonValue Value);
