using System.Security.Cryptography;
using System.Text;
using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database;

public sealed class ServiceUserStore(AbstractCoreContext context, TimeProvider timeProvider) : IServiceUserStore
{
    public async Task<IReadOnlyList<ServiceUserEntry>> List(bool deleted, CancellationToken cancellationToken)
    {
        using var scope = deleted ? context.SoftDelete.IncludeDeleted() : null;
        var state = deleted ? DeletionState.Deleted : DeletionState.Active;
        return await Project(context.ServiceUsers.InDeletionState(context, AbstractCoreContext.DeletionRecorded, state)
                .OrderBy(user => user.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<ServiceUserEntry>> Find(Guid id, CancellationToken cancellationToken) =>
        (await Project(context.ServiceUsers.Where(user => EF.Property<Guid>(user, "Id") == id))
            .ToListAsync(cancellationToken)).Head();

    public async Task<Option<ServiceUserEntry>> Authenticate(string apiKey, CancellationToken cancellationToken)
    {
        if (apiKey is null || apiKey.Length != 83 || !apiKey.StartsWith("behind_", StringComparison.Ordinal)
            || apiKey[39] != '_' || !Guid.TryParseExact(apiKey.AsSpan(7, 32), "N", out var id))
        {
            return None;
        }

        var found = await Find(id, cancellationToken);
        return await found.Match(
            () => Task.FromResult<Option<ServiceUserEntry>>(None),
            async entry =>
            {
                if (!entry.User.Enabled || await context.WallOfShameEntries.ForUser(id)
                        .ActiveAt(timeProvider.GetUtcNow()).AnyAsync(cancellationToken))
                {
                    return None;
                }

                var stored = Convert.FromHexString(entry.User.ApiKeyHash);
                var candidate = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
                if (stored.Length != candidate.Length || !CryptographicOperations.FixedTimeEquals(stored, candidate))
                {
                    return None;
                }

                var access = await context.GetUserAccessAsync(id, cancellationToken);
                return Some(entry with { User = entry.User with { Roles = Some<IEnumerable<Role>>(access.Roles) } });
            });
    }

    public async Task<Option<ServiceUserCredential>> Create(string name, CancellationToken cancellationToken)
    {
        if (!ServiceUser.IsValidName(name))
        {
            return None;
        }

        var id = Guid.CreateVersion7();
        var key = NewKey(id);
        var user = new ServiceUser(name.Trim(), Hash(key), true);
        await context.ServiceUsers.AddAsync(user, cancellationToken);
        context.Entry(user).Property<Guid>("Id").CurrentValue = id;
        context.Entry(user).Property<long>("Version").CurrentValue = 1;
        await context.Set<Dictionary<string, object>>("UserRoles").AddAsync(new Dictionary<string, object>
        {
            ["UserId"] = id,
            ["RoleId"] = BuiltInAccess.Users.Id,
            ["IsManuallyAssigned"] = true
        }, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return Some(new ServiceUserCredential(new ServiceUserEntry(id, 1, user), key));
    }

    public async Task<Option<ServiceUserCredential>> Rotate(Guid id, long version, CancellationToken cancellationToken)
    {
        var key = NewKey(id);
        var updated = await Change(id, version, user => user with { ApiKeyHash = Hash(key) }, false, cancellationToken);
        return updated.Map(user => new ServiceUserCredential(user, key));
    }

    public async Task<bool> SetEnabled(Guid id, long version, bool enabled, CancellationToken cancellationToken) =>
        (await Change(id, version, user => user with { Enabled = enabled }, false, cancellationToken))
        .Match(() => false, _ => true);

    public async Task<bool> Delete(Guid id, long version, CancellationToken cancellationToken) =>
        (await Change(id, version, user => user, true, cancellationToken)).Match(() => false, _ => true);

    public Task<bool> Restore(Guid id, long version, CancellationToken cancellationToken) => ChangeDeletion(id, version, false, cancellationToken);

    public Task<bool> HardDelete(Guid id, long version, CancellationToken cancellationToken) => ChangeDeletion(id, version, true, cancellationToken);

    public async Task<bool> SetAccess(
        Guid id,
        long version,
        IReadOnlyCollection<Guid> roleIds,
        AccessSelection selection,
        CancellationToken cancellationToken)
    {
        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (!await Lock(id, version, cancellationToken))
        {
            return false;
        }

        await context.ReplaceUserAccessAsync(id, roleIds, selection, cancellationToken);
        var user = await context.ServiceUsers.SingleAsync(
            value => EF.Property<Guid>(value, "Id") == id, cancellationToken);
        context.Entry(user).Property<long>("Version").CurrentValue = version + 1;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return false;
        }
    }

    private async Task<Option<ServiceUserEntry>> Change(
        Guid id, long version, Func<ServiceUser, ServiceUser> change, bool delete, CancellationToken cancellationToken)
    {
        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        if (!await Lock(id, version, cancellationToken))
        {
            return None;
        }

        var user = await context.ServiceUsers.SingleAsync(value => EF.Property<Guid>(value, "Id") == id, cancellationToken);
        var entry = context.Entry(user);
        await entry.ReloadAsync(cancellationToken);
        var updated = change(user);
        if (delete)
        {
            context.ServiceUsers.Remove(user);
        }
        else
        {
            entry.CurrentValues.SetValues(updated);
            entry.Property<long>("Version").CurrentValue = version + 1;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return None;
        }

        return Some(new ServiceUserEntry(id, delete ? version : version + 1, updated));
    }

    private async Task<bool> ChangeDeletion(Guid id, long version, bool hard, CancellationToken cancellationToken)
    {
        await using var transaction = context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        using var scope = context.SoftDelete.IncludeDeleted();
        var rows = context.ServiceUsers.Where(user => EF.Property<Guid>(user, "Id") == id);
        var found = await Project(rows).ToListAsync(cancellationToken);
        if (found.Count == 0 || found[0].Version != version || !await Lock(id, version, cancellationToken))
        {
            return false;
        }

        var deleted = await context.DeletedRowsAsync(rows, cancellationToken);
        if (deleted.Count == 0)
        {
            return false;
        }

        var user = deleted.Single();
        if (hard)
        {
            if (await context.IsUserReferencedByHistoryAsync(id, cancellationToken))
            {
                return false;
            }
            await context.RemoveUserAccessAsync(id, cancellationToken);
            context.HardDelete(user);
        }
        else
        {
            await context.RestoreByKeyAsync<ServiceUser>([id], cancellationToken);
            var entry = context.Entry(user);
            entry.CurrentValues.SetValues(user with { Enabled = false, ApiKeyHash = Hash(NewKey(id)) });
            entry.Property<long>("Version").CurrentValue = version + 1;
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return false;
        }
    }

    private async Task<bool> Lock(Guid id, long version, CancellationToken cancellationToken) =>
        await context.ServiceUsers.Where(user => EF.Property<Guid>(user, "Id") == id
                && EF.Property<long>(user, "Version") == version)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.Enabled, user => user.Enabled), cancellationToken) == 1;

    private static IQueryable<ServiceUserEntry> Project(IQueryable<ServiceUser> users) =>
        users.AsNoTracking().Select(user => new ServiceUserEntry(
            EF.Property<Guid>(user, "Id"), EF.Property<long>(user, "Version"), user));

    private static string NewKey(Guid id) =>
        $"behind_{id:N}_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";

    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
