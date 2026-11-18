using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public sealed class OidcAccountStore(AbstractCoreContext context) : IOidcAccountStore
{
    private const int MaxSynchronizationAttempts = 5;
    private const string SerializationFailure = "40001";
    private const string DeadlockDetected = "40P01";

    public async Task<Guid?> FindUser(
        string providerId,
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        ValidateIdentifier(providerId, nameof(providerId));
        ValidateIdentifier(issuer, nameof(issuer));
        ValidateIdentifier(subject, nameof(subject));

        var binding = await Bindings()
            .Where(row => EF.Property<string>(row, "ProviderDigest") == Digest(providerId)
                          && EF.Property<string>(row, "IssuerDigest") == Digest(issuer)
                          && EF.Property<string>(row, "SubjectDigest") == Digest(subject))
            .Select(row => (Guid?)EF.Property<Guid>(row, "UserId"))
            .FirstOrDefaultAsync(cancellationToken);

        return binding;
    }

    public async Task Link(
        Guid userId,
        string providerId,
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        ValidateIdentifier(providerId, nameof(providerId));
        ValidateIdentifier(issuer, nameof(issuer));
        ValidateIdentifier(subject, nameof(subject));

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LinkInTransaction(userId, providerId, issuer, subject, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> CreateAndLink(
        Func<Task<bool>> create,
        Guid userId,
        string providerId,
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(create);
        ValidateIdentifier(providerId, nameof(providerId));
        ValidateIdentifier(issuer, nameof(issuer));
        ValidateIdentifier(subject, nameof(subject));

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (!await create())
            {
                await transaction.RollbackAsync(cancellationToken);
                context.ChangeTracker.Clear();
                return false;
            }

            await LinkInTransaction(userId, providerId, issuer, subject, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            context.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task LinkInTransaction(
        Guid userId,
        string providerId,
        string issuer,
        string subject,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveUser(userId, cancellationToken))
        {
            throw new InvalidOperationException("An active user is required to link an OIDC account.");
        }

        var providerDigest = Digest(providerId);
        var issuerDigest = Digest(issuer);
        var subjectDigest = Digest(subject);
        var existingUser = await Bindings()
            .Where(row => EF.Property<string>(row, "ProviderDigest") == providerDigest
                          && EF.Property<string>(row, "IssuerDigest") == issuerDigest
                          && EF.Property<string>(row, "SubjectDigest") == subjectDigest)
            .Select(row => (Guid?)EF.Property<Guid>(row, "UserId"))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingUser is not null && existingUser != userId)
        {
            throw new InvalidOperationException("The OIDC account is already linked to another user.");
        }

        if (existingUser is null)
        {
            await Bindings().AddAsync(
                new Dictionary<string, object>
                {
                    ["ProviderDigest"] = providerDigest,
                    ["IssuerDigest"] = issuerDigest,
                    ["SubjectDigest"] = subjectDigest,
                    ["UserId"] = userId
                },
                cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SynchronizeRoles(
        Guid userId,
        string providerId,
        IReadOnlyList<string> mappedRoleNames,
        IReadOnlyList<string> autoRoleNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mappedRoleNames);
        ArgumentNullException.ThrowIfNull(autoRoleNames);
        ValidateIdentifier(providerId, nameof(providerId));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);
                await SynchronizeRolesInTransaction(userId, providerId, mappedRoleNames, autoRoleNames, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < MaxSynchronizationAttempts && IsSerializationConflict(exception))
            {
                context.ChangeTracker.Clear();
            }
        }
    }

    private async Task SynchronizeRolesInTransaction(
        Guid userId,
        string providerId,
        IReadOnlyList<string> mappedRoleNames,
        IReadOnlyList<string> autoRoleNames,
        CancellationToken cancellationToken)
    {
        if (!await IsActiveUser(userId, cancellationToken))
        {
            throw new InvalidOperationException("An active user is required to synchronize OIDC roles.");
        }

        var roles = await context.Roles
            .Select(role => new { Id = EF.Property<Guid>(role, "Id"), role.Name })
            .ToListAsync(cancellationToken);
        var rolesByName = roles
            .GroupBy(role => role.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var desiredRoleIds = new HashSet<Guid>();

        foreach (var roleName in NormalizeRoleNames(mappedRoleNames))
        {
            if (!rolesByName.TryGetValue(roleName, out var matches) || matches.Count != 1)
            {
                throw new InvalidOperationException($"Mapped OIDC role '{roleName}' must identify one active role.");
            }

            desiredRoleIds.Add(matches[0].Id);
        }

        foreach (var roleName in NormalizeRoleNames(autoRoleNames))
        {
            if (rolesByName.TryGetValue(roleName, out var matches))
            {
                if (matches.Count != 1 || !await IsOrdinaryRole(matches[0].Id, cancellationToken))
                {
                    throw new InvalidOperationException(
                        $"Auto-created OIDC role '{roleName}' must be an ordinary role without grants or restrictions.");
                }

                desiredRoleIds.Add(matches[0].Id);
                continue;
            }

            if (await HasDeletedRoleNamed(roleName, cancellationToken))
            {
                throw new InvalidOperationException($"Auto-created OIDC role '{roleName}' is deleted.");
            }

            var role = new UserRole(roleName);
            await context.Roles.AddAsync(role, cancellationToken);
            var roleId = context.Entry(role).Property<Guid>("Id").CurrentValue;
            await context.SaveChangesAsync(cancellationToken);
            rolesByName[roleName] = [new { Id = roleId, role.Name }];
            desiredRoleIds.Add(roleId);
        }

        var providerDigest = Digest(providerId);
        var ownedRoles = await Ownerships()
            .Where(row => EF.Property<Guid>(row, "UserId") == userId
                          && EF.Property<string>(row, "ProviderDigest") == providerDigest)
            .ToListAsync(cancellationToken);
        var oldRoleIds = ownedRoles
            .Select(row => (Guid)row["RoleId"])
            .ToHashSet();
        var rolesToRemove = oldRoleIds.Except(desiredRoleIds).ToHashSet();
        Ownerships().RemoveRange(ownedRoles.Where(row => rolesToRemove.Contains(
            (Guid)row["RoleId"])));

        var memberships = Memberships();
        var existingMemberships = await memberships
            .Where(row => EF.Property<Guid>(row, "UserId") == userId)
            .ToListAsync(cancellationToken);
        var membershipByRole = existingMemberships.ToDictionary(
            row => (Guid)row["RoleId"]);

        foreach (var roleId in desiredRoleIds)
        {
            if (!oldRoleIds.Contains(roleId))
            {
                await Ownerships().AddAsync(
                    new Dictionary<string, object>
                    {
                        ["ProviderDigest"] = providerDigest,
                        ["UserId"] = userId,
                        ["RoleId"] = roleId
                    },
                    cancellationToken);
            }

            if (!membershipByRole.ContainsKey(roleId))
            {
                var membership = new Dictionary<string, object>
                {
                    ["UserId"] = userId,
                    ["RoleId"] = roleId,
                    ["IsManuallyAssigned"] = false
                };
                await memberships.AddAsync(membership, cancellationToken);
                membershipByRole[roleId] = membership;
            }
        }

        foreach (var roleId in rolesToRemove)
        {
            if (!membershipByRole.TryGetValue(roleId, out var membership)
                || (bool)membership["IsManuallyAssigned"])
            {
                continue;
            }

            var anotherProviderOwnsRole = await Ownerships().AnyAsync(
                row => EF.Property<Guid>(row, "UserId") == userId
                       && EF.Property<Guid>(row, "RoleId") == roleId
                       && EF.Property<string>(row, "ProviderDigest") != providerDigest,
                cancellationToken);

            if (!anotherProviderOwnsRole)
            {
                memberships.Remove(membership);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsSerializationConflict(Exception exception) => exception switch
    {
        DbUpdateException { InnerException: { } inner } => IsSerializationConflict(inner),
        DbException { SqlState: SerializationFailure or DeadlockDetected } => true,
        _ when RoleNameUniqueness.IsConflict(exception) => true,
        _ => false
    };

    private DbSet<Dictionary<string, object>> Bindings() =>
        context.Set<Dictionary<string, object>>("OidcAccountBindings");

    private DbSet<Dictionary<string, object>> Ownerships() =>
        context.Set<Dictionary<string, object>>("OidcRoleOwnerships");

    private DbSet<Dictionary<string, object>> Memberships() =>
        context.Set<Dictionary<string, object>>("UserRoles");

    private async Task<bool> IsActiveUser(Guid userId, CancellationToken cancellationToken)
    {
        var user = await context.Users.FirstOrDefaultAsync(
            user => EF.Property<Guid>(user, "Id") == userId,
            cancellationToken);

        return user is not null && !await context.Entry(user).IsDeletedAsync(cancellationToken);
    }

    private async Task<bool> IsOrdinaryRole(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await context.Roles.FirstOrDefaultAsync(
            candidate => EF.Property<Guid>(candidate, "Id") == roleId,
            cancellationToken);

        if (role is null || role.IsDeveloper)
        {
            return false;
        }

        return !await context.Set<Dictionary<string, object>>("RoleGrants")
                   .AnyAsync(row => EF.Property<Guid>(row, "RoleId") == roleId, cancellationToken)
               && !await context.Set<Dictionary<string, object>>("RoleRestrictions")
                   .AnyAsync(row => EF.Property<Guid>(row, "RoleId") == roleId, cancellationToken);
    }

    private async Task<bool> HasDeletedRoleNamed(string roleName, CancellationToken cancellationToken)
    {
        using var scope = context.SoftDelete.IncludeDeleted();
        var roles = await context.Roles.Where(role => role.Name == roleName).ToListAsync(cancellationToken);

        foreach (var role in roles.Where(role =>
                     string.Equals(role.Name, roleName, StringComparison.Ordinal)))
        {
            if (await context.Entry(role).IsDeletedAsync(cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> NormalizeRoleNames(IReadOnlyList<string> names)
    {
        var normalized = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in names)
        {
            ValidateIdentifier(name, nameof(names));
            var trimmed = name.Trim();

            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        return normalized;
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("OIDC identifiers must not be empty.", parameterName);
        }
    }

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
