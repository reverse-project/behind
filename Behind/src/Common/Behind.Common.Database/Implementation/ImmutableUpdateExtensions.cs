using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Behind.Common.Database.Implementation;

public static class ImmutableUpdateExtensions
{
    extension(DbContext dbContext)
    {
        public void UpdateImmutable<T>(T modified) where T : class
        {
            var key = dbContext.GetStableKey(modified);
            var entry = dbContext.Set<T>().Local.FindEntry(key.EntityKey.Properties, key.Values);
            if (entry is null)
            {
                throw new InvalidOperationException("Original entity must be tracked.");
            }

            entry.State = EntityState.Detached;
            dbContext.Attach(modified);
            dbContext.Entry(modified).State = EntityState.Modified;

            // Align navigation graphs with the immutable copy
            foreach (var navigation in entry.Navigations)
            {
                switch (navigation)
                {
                    case CollectionEntry collectionEntry:
                        dbContext.UpdateCollection(collectionEntry, modified);
                        break;
                    case ReferenceEntry referenceEntry:
                        dbContext.UpdateReference(referenceEntry, modified);
                        break;
                }
            }
        }
        
        public void UpdateImmutable<T>(object[] keyValues, T modified) where T : class
        {
            var entityType = dbContext.Model.FindEntityType(typeof(T))
                             ?? throw new InvalidOperationException($"Entity type {typeof(T).FullName} not found.");

            var primaryKey = entityType.FindPrimaryKey()
                             ?? throw new InvalidOperationException($"Entity type {typeof(T).FullName} has no primary key.");

            if (primaryKey.Properties.Count != keyValues.Length)
            {
                throw new InvalidOperationException(
                    $"Expected {primaryKey.Properties.Count} key values but received {keyValues.Length}.");
            }

            var original = dbContext.Set<T>().Find(keyValues);

            if (original is null)
            {
                throw new InvalidOperationException("Original entity must exist.");
            }

            var entry = dbContext.Entry(original);
            var navigations = entry.Navigations.ToList();
            var shadowValues = entityType.GetProperties()
                .Where(property => property.IsShadowProperty() && !property.IsPrimaryKey())
                .ToDictionary(property => property.Name, property => entry.Property(property.Name).CurrentValue);

            entry.State = EntityState.Detached;

            dbContext.Attach(modified);

            var modifiedEntry = dbContext.Entry(modified);

            foreach (var (name, value) in shadowValues)
            {
                modifiedEntry.Property(name).CurrentValue = value;
            }

            for (int i = 0; i < primaryKey.Properties.Count; i++)
            {
                modifiedEntry.Property(primaryKey.Properties[i].Name).CurrentValue = keyValues[i];
            }

            modifiedEntry.State = EntityState.Modified;

            // Align navigation graphs with the immutable copy
            foreach (var navigation in navigations)
            {
                switch (navigation)
                {
                    case CollectionEntry collectionEntry:
                        dbContext.UpdateCollection(collectionEntry, modified);
                        break;
                    case ReferenceEntry referenceEntry:
                        dbContext.UpdateReference(referenceEntry, modified);
                        break;
                }
            }
        }

        private void UpdateCollection(CollectionEntry collectionEntry,
            object modifiedEntity)
        {
            var trackedCollection = collectionEntry.CurrentValue as IList;
            var modifiedCollection = GetCollectionValue(modifiedEntity, collectionEntry.Metadata);

            if (trackedCollection is null || modifiedCollection is null)
            {
                return;
            }

            var key = collectionEntry.Metadata.TargetEntityType.FindPrimaryKey();
            if (key is null)
            {
                throw new InvalidOperationException("Unable to determine key properties for collection items.");
            }

            if (key.Properties.Any(x => x.IsShadowProperty()))
            {
                throw new InvalidOperationException("Cannot update collections with shadow key properties.");
            }

            var originalEntries = trackedCollection
                .OfType<object>()
                .Select(item => (item, key: item.TryGetStableKey(key)))
                .Where(pair => pair.key is not null)
                .ToDictionary(pair => pair.key!, pair => pair.item);

            var updateMethod = GetUpdateImmutableMethod(collectionEntry.Metadata.TargetEntityType.ClrType);

            foreach (var modifiedItem in modifiedCollection.OfType<object>())
            {
                var keyValue = modifiedItem.TryGetStableKey(key);
                if (keyValue is not null && originalEntries.ContainsKey(keyValue))
                {
                    updateMethod.Invoke(null, [dbContext, modifiedItem]);
                }
                else
                {
                    dbContext.Entry(modifiedItem).State = EntityState.Added;
                }
            }

            foreach (var itemToRemove in originalEntries.Values)
            {
                dbContext.Remove(itemToRemove);
            }
        }

        private CompositeKey GetStableKey(object entity) =>
            dbContext.TryGetStableKey(entity)
            ?? throw new InvalidOperationException("Entity must have a valid stable key.");

        private CompositeKey? TryGetStableKey(object entity)
        {
            var key = dbContext.Model.FindEntityType(entity.GetType())?.FindPrimaryKey();
            return key is null
                ? throw new InvalidOperationException("Unable to determine primary key for entity.")
                : entity.TryGetStableKey(key);
        }

        private void UpdateReference(ReferenceEntry referenceEntry, object modifiedEntity)
        {
            var modifiedReference = GetNavigationValue(modifiedEntity, referenceEntry.Metadata);
            if (modifiedReference is null)
            {
                return;
            }

            if (referenceEntry.TargetEntry is { Entity: { } })
            {
                var updateMethod = GetUpdateImmutableMethod(referenceEntry.Metadata.TargetEntityType.ClrType);
                updateMethod.Invoke(null, [dbContext, modifiedReference]);
                return;
            }

            referenceEntry.CurrentValue = modifiedReference; // not tracked - start tracking it

            var key = referenceEntry.Metadata.TargetEntityType.FindPrimaryKey()
                      ?? throw new InvalidOperationException("Unable to determine entity key for reference target.");

            var stableKey = modifiedReference.TryGetStableKey(key);
            dbContext.Entry(modifiedReference).State = stableKey is null ? EntityState.Added : EntityState.Unchanged;
        }
    }

    private static MethodInfo GetUpdateImmutableMethod(Type entityType) =>
        typeof(ImmutableUpdateExtensions)
            .GetMethod(nameof(UpdateImmutable), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(entityType)
        ?? throw new InvalidOperationException("Unable to get UpdateImmutable method.");

    private static CompositeKey? TryGetStableKey(this object entity, IKey key)
    {
        var keyProperties = key.Properties;
        var values = new object[keyProperties.Count];

        for (int i = 0; i < keyProperties.Count; i++)
        {
            var prop = keyProperties[i];
            if (prop.IsShadowProperty())
            {
                throw new InvalidOperationException($"Cannot get key value for shadow property {prop.Name}.");
            }

            var propertyInfo = prop.PropertyInfo
                               ?? entity.GetType().GetProperty(prop.Name,
                                   BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                               ?? throw new InvalidOperationException(
                                   $"Key property '{prop.Name}' not found on type '{entity.GetType().Name}'.");

            var value = propertyInfo.GetValue(entity);

            var defaultValue = prop.ClrType.IsValueType ? Activator.CreateInstance(prop.ClrType) : null;
            if (Equals(value, defaultValue))
            {
                return null;
            }

            values[i] = value!;
        }

        return new CompositeKey(key, values.ToArray());
    }

    private record CompositeKey(IKey EntityKey, object[] Values)
    {
        public virtual bool Equals(CompositeKey? other) =>
            other is not null && Values.SequenceEqual(other.Values);

        public override int GetHashCode()
        {
            var hc = new HashCode();
            foreach (var v in Values)
            {
                hc.Add(v);
            }
            return hc.ToHashCode();
        }
    }

    extension(object source)
    {
        private IEnumerable? GetCollectionValue(INavigationBase navigation) => source.GetNavigationValue(navigation) as IEnumerable;

        private object? GetNavigationValue(INavigationBase navigation) =>
            navigation.PropertyInfo is { } propertyInfo ? propertyInfo.GetValue(source)
            : navigation.FieldInfo is { } fieldInfo ? fieldInfo.GetValue(source)
            : null;
    }
}