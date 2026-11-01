using Behind.Core.Database;
using Behind.Core.Database.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.Helpers;

/// <summary>
/// Helpers that insert well-formed domain entities into an in-memory test database.
/// Every helper method returns the generated shadow "Id" so callers can reference
/// the seeded rows in follow-up queries.
/// </summary>
public static class DatabaseSeedHelper
{
    // -------------------------------------------------------------------------
    // User / Email
    // -------------------------------------------------------------------------

    public static Task EnsureBuiltInUsersAsync(CoreContextSqlite context) =>
        context.EnsureBuiltInUsersAsync();

    /// <summary>Inserts a HumanUser row and one Email row for it.</summary>
    public static async Task<Guid> SeedHumanUserAsync(
        CoreContextSqlite context,
        string emailValue = "user@test.com")
    {
        var user = new HumanUser("Test User", [new Email(emailValue)], None);

        await context.Users.AddAsync(user);

        // Uuid7ValueGenerator.GeneratesTemporaryValues == false → ID is final immediately after Add
        var userId = context.Entry(user).Property<Guid>("Id").CurrentValue;

        await context.SaveChangesAsync();
        return userId;
    }

    /// <summary>Inserts a second Email row for an existing user.</summary>
    public static async Task<Guid> SeedEmailAsync(
        CoreContextSqlite context,
        Guid userId,
        string emailValue)
    {
        var email = new Email(emailValue);
        await context.Emails.AddAsync(email);
        context.Entry(email).Property<Guid>("UserId").CurrentValue = userId;

        await context.SaveChangesAsync();
        return context.Entry(email).Property<Guid>("Id").CurrentValue;
    }

    // -------------------------------------------------------------------------
    // Grant
    // -------------------------------------------------------------------------

    public static async Task<Guid> SeedGrantAsync(CoreContextSqlite context, string name = "read")
    {
        var grant = new Grant(name);
        await context.Grants.AddAsync(grant);
        await context.SaveChangesAsync();
        return context.Entry(grant).Property<Guid>("Id").CurrentValue;
    }

    // -------------------------------------------------------------------------
    // Role
    // -------------------------------------------------------------------------

    public static async Task<Guid> SeedUserRoleAsync(CoreContextSqlite context, string name = "viewer")
    {
        var role = new UserRole(name);
        await context.Roles.AddAsync(role);
        await context.SaveChangesAsync();
        return context.Entry(role).Property<Guid>("Id").CurrentValue;
    }

    // -------------------------------------------------------------------------
    // Join tables (shadow entities)
    // -------------------------------------------------------------------------

    public static async Task AssignRoleToUserAsync(
        CoreContextSqlite context,
        Guid userId,
        Guid roleId)
    {
        await context.Set<Dictionary<string, object>>("UserRoles").AddAsync(
            new Dictionary<string, object>
            {
                ["UserId"] = userId,
                ["RoleId"] = roleId
            });

        await context.SaveChangesAsync();
    }

    public static async Task AssignGrantToRoleAsync(
        CoreContextSqlite context,
        Guid roleId,
        Guid grantId)
    {
        await context.Set<Dictionary<string, object>>("RoleGrants").AddAsync(
            new Dictionary<string, object>
            {
                ["RoleId"] = roleId,
                ["GrantId"] = grantId
            });

        await context.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------
    // AuditLogEntry
    // -------------------------------------------------------------------------

    public static async Task<Guid> SeedAuditLogEntryAsync(
        CoreContextSqlite context,
        Guid? userId = null,
        Guid? behalfOfId = null,
        DateTimeOffset? timestamp = null,
        string entityName = "Entity",
        string entityKey = "key-1")
    {
        if (userId is not null || behalfOfId is not null)
        {
            await EnsureBuiltInUsersAsync(context);
        }

        var ts = timestamp ?? DateTimeOffset.UtcNow;
        var entry = new AuditLogEntry(entityName, entityKey, new AuditLogEntry.Created(), ts);
        await context.AuditLogEntries.AddAsync(entry);

        if (userId.HasValue)
        {
            context.Entry(entry).Property("UserId").CurrentValue = userId.Value;
        }

        if (behalfOfId.HasValue)
        {
            context.Entry(entry).Property("BehalfOfId").CurrentValue = behalfOfId.Value;
        }

        await context.SaveChangesAsync();
        return context.Entry(entry).Property<Guid>("Id").CurrentValue;
    }

    // -------------------------------------------------------------------------
    // WallOfShameEntry
    // -------------------------------------------------------------------------

    /// <summary>
    /// Inserts a WallOfShameEntry. UserId and GivenById must reference existing users.
    /// Built-in users are seeded automatically for the constant system/guest ids.
    /// </summary>
    public static async Task<Guid> SeedWallOfShameEntryAsync(
        CoreContextSqlite context,
        Guid userId,
        Guid givenById,
        string reason = "spam",
        DateTimeOffset? expiresAt = null)
    {
        await EnsureBuiltInUsersAsync(context);

        var entry = new WallOfShameEntry(new Reason(reason));
        await context.WallOfShameEntries.AddAsync(entry);

        context.Entry(entry).Property("UserId").CurrentValue = userId;
        context.Entry(entry).Property("GivenById").CurrentValue = givenById;

        if (expiresAt.HasValue)
        {
            context.Entry(entry).Property("ExpiresAt").CurrentValue = expiresAt.Value;
        }

        await context.SaveChangesAsync();
        return context.Entry(entry).Property<Guid>("Id").CurrentValue;
    }

    // -------------------------------------------------------------------------
    // Session
    // -------------------------------------------------------------------------

    public static async Task<Guid> SeedSessionAsync(
        CoreContextSqlite context,
        ValidSession session,
        Guid userId,
        Guid? invalidatedById = null,
        DateTimeOffset? invalidatedAt = null)
    {
        await EnsureBuiltInUsersAsync(context);

        await ((IRepository<Session, Guid>)context).AddAsync(session);

        var entry = context.Entry(session);
        entry.Property("UserId").CurrentValue = userId;
        entry.Property("InvalidatedById").CurrentValue = invalidatedById;
        entry.Property("InvalidatedAt").CurrentValue = invalidatedAt;

        await context.SaveChangesAsync();
        return entry.Property<Guid>("Id").CurrentValue;
    }
}