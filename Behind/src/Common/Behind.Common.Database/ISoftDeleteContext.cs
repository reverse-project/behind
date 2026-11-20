namespace Behind.Common.Database;

public interface ISoftDeleteContext
{
    SoftDeleteSwitch SoftDelete { get; }

    EntityMarkRegistry HardDeletes { get; }

    EntityMarkRegistry Restores { get; }

    Task<DateTimeOffset?> DeletedAtAsync(string entityName, string entityKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(null);

    Task<bool> IsDeletedAsync(string entityName, string entityKey, CancellationToken cancellationToken = default);
}
