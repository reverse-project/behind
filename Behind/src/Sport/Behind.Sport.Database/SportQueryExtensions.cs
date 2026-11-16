using Behind.Common.Database;
using Behind.Sport.Domain;

namespace Behind.Sport.Database;

public static class SportQueryExtensions
{
    extension(ISportContext context)
    {
        public IQueryable<TEntity> ActiveLatest<TEntity>(IQueryable<TEntity> set)
            where TEntity : class =>
            context.InDeletionState(set, DeletionState.Active).LatestOf(set);
    }

    extension<TValue>(IReadOnlyDictionary<Guid, TValue> rows) where TValue : class
    {
        public TValue? Find(Guid? id) =>
            id is { } key && rows.TryGetValue(key, out var found) ? found : default;
    }

    extension(Season season)
    {
        public DateOnly Recency => season.StartsOn ?? season.EndsOn ?? DateOnly.MinValue;
    }

    extension(DateOnly birthDate)
    {
        public int AgeOn(DateOnly today) =>
            today.Year - birthDate.Year - (today < birthDate.AddYears(today.Year - birthDate.Year) ? 1 : 0);
    }
}
