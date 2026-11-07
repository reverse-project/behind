namespace Behind.Common.Database.Modules;

public interface IAuditLabelSource
{
    Task<IReadOnlyList<AuditLabel>> LabelsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
