namespace Behind.Core.Database.Auditing;

public sealed class AutonomousAuditActorProvider : IAuditActorProvider
{
    public Task<AuditActor> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AuditActor.Autonomous);
}
