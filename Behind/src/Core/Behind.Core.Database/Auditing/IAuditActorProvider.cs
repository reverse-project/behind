namespace Behind.Core.Database.Auditing;

public interface IAuditActorProvider
{
    Task<AuditActor> GetCurrentAsync(CancellationToken cancellationToken = default);
}
