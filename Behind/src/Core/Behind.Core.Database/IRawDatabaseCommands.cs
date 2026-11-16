using System.Text.Json;

namespace Behind.Core.Database;

public interface IRawDatabaseCommands
{
    Task<RawWriteResult> Insert(
        string table,
        IReadOnlyDictionary<string, JsonElement> values,
        CancellationToken cancellationToken = default);

    Task<RawWriteResult> Update(
        string table,
        IReadOnlyDictionary<string, JsonElement> key,
        IReadOnlyDictionary<string, JsonElement> values,
        CancellationToken cancellationToken = default);

    Task<RawWriteResult> Delete(
        string table,
        IReadOnlyDictionary<string, JsonElement> key,
        CancellationToken cancellationToken = default);
}
