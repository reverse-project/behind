using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Queries;

public sealed class RoleQueries(AbstractCoreContext context) : IRoleQueries
{
    public async Task<Option<IRoleQueries.RoleResult>> GetRoleByName(string name,
        CancellationToken cancellationToken = default)
    {
        var found = await FindRoles(role => role.Name.ToLower() == name.ToLower(), cancellationToken);

        return found.Head();
    }

    public async Task<Option<IRoleQueries.RoleResult>> GetRoleById(Guid id,
        CancellationToken cancellationToken = default)
    {
        var found = await FindRoles(role => EF.Property<Guid>(role, "Id") == id, cancellationToken);

        return found.Head();
    }

    public Task<IReadOnlyList<IRoleQueries.RoleResult>> ListRoles(CancellationToken cancellationToken = default) =>
        FindRoles(_ => true, cancellationToken);

    public async Task<PagedResult<IRoleQueries.RoleResult>> PageRoles(
        Flipper flipper,
        Option<string> search,
        Option<string> letter,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);
        var matching = Matching(search, state).InBucket(role => role.Name, letter);
        var total = await matching.LongCountAsync(cancellationToken);

        var roles = await flipper.Apply(
                matching
                    .OrderBy(role => role.Name.ToLower())
                    .ThenBy(role => EF.Property<Guid>(role, "Id"))
                    .Select(role => new { Id = EF.Property<Guid>(role, "Id"), Role = role }))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var withAccess = await context.WithAccessAsync(
            roles.Select(role => (role.Id, (Role)role.Role)).ToList(),
            cancellationToken);

        var results = roles
            .Select((role, index) => new IRoleQueries.RoleResult(role.Id, withAccess[index]))
            .ToList();

        return new PagedResult<IRoleQueries.RoleResult>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            results.Count > 0 ? Some<IEnumerable<IRoleQueries.RoleResult>>(results) : None);
    }

    public async Task<IReadOnlyList<string>> GetLetters(
        Option<string> search,
        DeletionState state,
        CancellationToken cancellationToken = default)
    {
        using var scope = Scope(state);

        return await Matching(search, state).LettersAsync(role => role.Name, cancellationToken);
    }

    private IDisposable? Scope(DeletionState state) =>
        state == DeletionState.Deleted ? context.SoftDelete.IncludeDeleted() : null;

    private IQueryable<UserRole> Matching(Option<string> search, DeletionState state)
    {
        var roles = context.Roles.InDeletionState(context, AbstractCoreContext.DeletionRecorded, state);

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => roles,
                term => roles.Where(role => role.Name.ToLower().Contains(term)));
    }

    public async Task<IReadOnlyList<Role>> GetRolesByUserId(Guid userId,
        CancellationToken cancellationToken = default) =>
        await context.WithAccessAsync(await context.FindRolesOfUserAsync(userId, cancellationToken), cancellationToken);

    private async Task<IReadOnlyList<IRoleQueries.RoleResult>> FindRoles(
        Expression<Func<UserRole, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var roles = await context.Roles
            .Where(predicate)
            .OrderBy(role => role.Name)
            .Select(role => new
            {
                Id = EF.Property<Guid>(role, "Id"),
                Role = role
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var withAccess = await context.WithAccessAsync(
            roles.Select(role => (role.Id, (Role)role.Role)).ToList(),
            cancellationToken);

        return roles
            .Select((role, index) => new IRoleQueries.RoleResult(role.Id, withAccess[index]))
            .ToList();
    }
}
