using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.Database.Queries;

public sealed class UserApprovalQueries(AbstractCoreContext context) : IUserApprovalQueries
{
    public Task<bool> IsPending(Guid userId, CancellationToken cancellationToken = default) =>
        Pending
            .Where(row => EF.Property<Guid>(row, UserApprovalStore.UserId) == userId)
            .AnyAsync(cancellationToken);

    public async Task<PagedResult<IUserApprovalQueries.PendingUser>> ListPending(
        Flipper flipper,
        Option<string> search,
        CancellationToken cancellationToken = default)
    {
        var matching = Matching(search);
        var total = await matching.LongCountAsync(cancellationToken);

        var rows = await flipper.Apply(
                matching
                    .OrderBy(user => user.DisplayName.ToLower())
                    .ThenBy(user => EF.Property<Guid>(user, "Id")))
            .Select(user => new PendingRow(EF.Property<Guid>(user, "Id"), user.DisplayName))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var emails = await EmailsOf(rows.Select(row => row.Id).ToList(), cancellationToken);

        var users = rows
            .Select(row => new IUserApprovalQueries.PendingUser(
                row.Id,
                row.DisplayName,
                emails.GetValueOrDefault(row.Id, [])))
            .ToList();

        return new PagedResult<IUserApprovalQueries.PendingUser>(
            total,
            flipper.Index,
            flipper.PageCount(total),
            flipper.Size,
            users.Count > 0 ? Some<IEnumerable<IUserApprovalQueries.PendingUser>>(users) : None);
    }

    private sealed record PendingRow(Guid Id, string DisplayName);

    private IQueryable<Dictionary<string, object>> Pending =>
        context.Set<Dictionary<string, object>>(UserApprovalStore.Table);

    private IQueryable<HumanUser> Matching(Option<string> search)
    {
        var people = context.Users
            .Where(user => Pending.Any(row =>
                EF.Property<Guid>(row, UserApprovalStore.UserId) == EF.Property<Guid>(user, "Id")));

        return search
            .Map(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length > 0)
            .Match(
                () => people,
                term => people.Where(user =>
                    user.DisplayName.ToLower().Contains(term)
                    || context.Emails.Any(email =>
                        EF.Property<Guid>(email, "UserId") == EF.Property<Guid>(user, "Id")
                        && email.Value.ToLower().Contains(term))));
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> EmailsOf(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken) =>
        (await context.Emails
            .Where(email => userIds.Contains(EF.Property<Guid>(email, "UserId")))
            .Select(email => new { UserId = EF.Property<Guid>(email, "UserId"), email.Value })
            .AsNoTracking()
            .ToListAsync(cancellationToken))
        .GroupBy(email => email.UserId)
        .ToDictionary(
            group => group.Key,
            group => (IReadOnlyList<string>)group.Select(email => email.Value).ToList());
}
