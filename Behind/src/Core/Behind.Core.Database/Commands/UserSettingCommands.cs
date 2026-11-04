using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class UserSettingCommands(AbstractCoreContext context) : IUserSettingCommands
{
    public async Task Set(Guid userId, string key, string value, CancellationToken cancellationToken = default)
    {
        var rows = context.Set<Dictionary<string, object>>(UserSettingStore.Table);

        var existing = await rows
            .Where(row => EF.Property<Guid>(row, UserSettingStore.UserId) == userId
                          && EF.Property<string>(row, UserSettingStore.Key) == key)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is null && value != string.Empty)
        {
            await rows.AddAsync(NewRow(userId, key, value), cancellationToken);
        }
        else if (existing is not null && value == string.Empty)
        {
            rows.Remove(existing);
        }
        else if (existing is not null)
        {
            context.Entry(existing).Property(UserSettingStore.Value).CurrentValue = value;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<string, object> NewRow(Guid userId, string key, string value) =>
        new()
        {
            [UserSettingStore.UserId] = userId,
            [UserSettingStore.Key] = key,
            [UserSettingStore.Value] = value
        };
}
