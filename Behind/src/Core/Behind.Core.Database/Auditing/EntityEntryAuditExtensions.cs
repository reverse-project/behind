using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Auditing;

public static class EntityEntryAuditExtensions
{
    public const string MaskedValue = "***";

    extension(EntityEntry entry)
    {
        public string AuditEntityName =>
            entry.Metadata.HasSharedClrType
                ? entry.Metadata.Name
                : entry.Metadata.GetRootType().ClrType.Name;

        public string AuditEntityKey =>
            string.Join(
                ':',
                entry.Metadata.FindPrimaryKey()!.Properties.Select(property => entry.Property(property.Name).CurrentValue)
            );

        public async Task<Option<AuditLogEntry.AuditAction>> ToAuditActionAsync(
            CancellationToken cancellationToken = default
        ) =>
            entry.State switch
            {
                EntityState.Added => Some<AuditLogEntry.AuditAction>(new AuditLogEntry.Created()),
                EntityState.Deleted when entry.IsHardDeleted => Some<AuditLogEntry.AuditAction>(new AuditLogEntry.HardDeleted()),
                EntityState.Deleted => Some<AuditLogEntry.AuditAction>(new AuditLogEntry.Deleted()),
                EntityState.Modified => await entry.ToUpdatedActionAsync(cancellationToken),
                _ => None
            };

        private IEnumerable<PropertyDifference> DifferencesFrom(PropertyValues stored) =>
            entry.CurrentValues.Properties
                .Select(property => (Property: property, Old: stored[property], New: entry.CurrentValues[property]))
                .Where(pair => !Equals(pair.Old, pair.New))
                .Select(pair => pair.Property.ToDifference(pair.Old, pair.New));

        private async Task<Option<AuditLogEntry.AuditAction>> ToUpdatedActionAsync(
            CancellationToken cancellationToken
        )
        {
            var stored = await entry.GetDatabaseValuesAsync(cancellationToken);

            if (stored is null)
            {
                return Some<AuditLogEntry.AuditAction>(new AuditLogEntry.Updated([]));
            }

            var differences = entry.DifferencesFrom(stored).ToList();

            return differences.Count == 0
                ? None
                : Some<AuditLogEntry.AuditAction>(new AuditLogEntry.Updated(differences));
        }
    }

    extension(IPropertyBase property)
    {
        private string QualifiedName =>
            property.DeclaringType is IComplexType complexType
                ? $"{complexType.ComplexProperty.QualifiedName}.{property.Name}"
                : property.Name;

        private PropertyDifference ToDifference(object? oldValue, object? newValue) =>
            property.PropertyInfo?.IsSensible == true
                ? new PropertyDifference(property.QualifiedName, MaskedValue, MaskedValue)
                : new PropertyDifference(property.QualifiedName, oldValue, newValue);
    }
}
