using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class UserSettingQueries(AbstractCoreContext context) : IUserSettingQueries
{
    public async Task<IReadOnlyDictionary<string, string>> GetAll(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        (await context.Set<Dictionary<string, object>>(UserSettingStore.Table)
            .Where(row => EF.Property<Guid>(row, UserSettingStore.UserId) == userId)
            .Select(row => new
            {
                Key = EF.Property<string>(row, UserSettingStore.Key),
                Value = EF.Property<string>(row, UserSettingStore.Value)
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
        .ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
}
