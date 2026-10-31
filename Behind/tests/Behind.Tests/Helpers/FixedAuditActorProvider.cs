using Behind.Core.Database.Auditing;

namespace Behind.Tests.Helpers;

public sealed class FixedAuditActorProvider(AuditActor actor) : IAuditActorProvider
{
    public Task<AuditActor> GetCurrentAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(actor);
}
