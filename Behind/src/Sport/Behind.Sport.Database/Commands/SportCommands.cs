using System.Linq.Expressions;
using Behind.Common;
using Behind.Common.Database;
using Microsoft.EntityFrameworkCore;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Sport.Database.Commands;

public abstract class SportCommands<TEntity, TDraft>(ISportContext context)
    : VersionedCommands<TEntity, TDraft>(context)
    where TEntity : class
{
    protected ISportContext Sport => context;

    protected static Option<string> Reject(bool rejected, string reason) => rejected ? Some(reason) : None;

    protected static async Task<Option<string>> FirstRejection(params Func<Task<Option<string>>>[] checks)
    {
        foreach (var check in checks)
        {
            var outcome = await check();

            if (outcome.Match(() => false, _ => true))
            {
                return outcome;
            }
        }

        return None;
    }

    protected async Task<Option<TReferenced>> Active<TReferenced>(
        IQueryable<TReferenced> set,
        Guid? key,
        CancellationToken cancellationToken)
        where TReferenced : class =>
        key is { } id
            ? (await Sport.ActiveLatest(set).OfKey(id).AsNoTracking().FirstOrDefaultAsync(cancellationToken))
            .ToOption()
            : None;

    protected async Task<bool> IsReferencedBy<TReferrer>(
        IQueryable<TReferrer> set,
        Expression<Func<TReferrer, bool>> predicate,
        CancellationToken cancellationToken)
        where TReferrer : class
    {
        using var scope = Sport.SoftDelete.IncludeDeleted();

        return await set.LatestOf(set).AnyAsync(predicate, cancellationToken);
    }
}
