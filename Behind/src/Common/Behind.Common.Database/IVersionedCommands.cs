namespace Behind.Common.Database;

public interface IVersionedCommands<TDraft>
{
    Task<VersionedSaveResult> Create(TDraft draft, CancellationToken cancellationToken = default);

    Task<VersionedSaveResult> Update(Guid key, TDraft draft, CancellationToken cancellationToken = default);

    Task<bool> Delete(Guid key, CancellationToken cancellationToken = default);

    Task<bool> Restore(Guid key, CancellationToken cancellationToken = default);

    Task<HardDeleteResult> HardDelete(Guid key, CancellationToken cancellationToken = default);

    Task<bool> Ensure(Guid key, TDraft draft, CancellationToken cancellationToken = default);
}
