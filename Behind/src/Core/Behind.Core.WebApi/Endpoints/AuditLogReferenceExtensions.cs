using Behind.Core.Database;
using Behind.Core.Domain;

namespace Behind.Core.WebApi.Endpoints;

public static class AuditLogReferenceExtensions
{
    extension(AuditLogEntry entry)
    {
        public IReadOnlyCollection<Guid> References =>
            entry.EntityKey.Split(AuditLabels.KeySeparator)
                .Concat(entry.Action.ToChanges().SelectMany(change => new[] { change.OldValue, change.NewValue }))
                .OfType<string>()
                .Select(text => Guid.TryParse(text, out var id) ? id : (Guid?)null)
                .OfType<Guid>()
                .Distinct()
                .ToList();
    }

    extension(IEnumerable<AuditLogEntry> entries)
    {
        public IReadOnlyCollection<Guid> References =>
            entries.SelectMany(entry => entry.References).Distinct().ToList();
    }
}
