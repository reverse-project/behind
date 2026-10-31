using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Behind.Common.Database;

internal static class SoftDeleteCascade
{
    private static readonly MethodInfo FindDependentsMethod =
        typeof(SoftDeleteCascade).GetMethod(nameof(FindDependentsAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static async Task<IReadOnlyList<EntityEntry>> FindDependentsAsync(
        DbContext context,
        EntityEntry principal,
        CancellationToken cancellationToken)
    {
        var dependents = new List<EntityEntry>();

        foreach (var foreignKey in CascadingForeignKeys(principal.Metadata))
        {
            var found = await (Task<List<object>>)FindDependentsMethod
                .MakeGenericMethod(foreignKey.DeclaringEntityType.ClrType)
                .Invoke(null, [context, foreignKey, principal, cancellationToken])!;

            dependents.AddRange(found.Select(context.Entry));
        }

        return dependents;
    }

    private static IEnumerable<IForeignKey> CascadingForeignKeys(IEntityType principal) =>
        principal.GetReferencingForeignKeys()
            .Where(foreignKey => foreignKey.DeleteBehavior == DeleteBehavior.Cascade)
            .Where(foreignKey => !foreignKey.DeclaringEntityType.HasSharedClrType);

    private static async Task<List<object>> FindDependentsAsync<TDependent>(
        DbContext context,
        IForeignKey foreignKey,
        EntityEntry principal,
        CancellationToken cancellationToken) where TDependent : class =>
        (await context.Set<TDependent>()
            .Where(ReferencesPrincipal<TDependent>(foreignKey, principal))
            .ToListAsync(cancellationToken))
        .Cast<object>()
        .ToList();

    private static Expression<Func<TDependent, bool>> ReferencesPrincipal<TDependent>(
        IForeignKey foreignKey,
        EntityEntry principal)
    {
        var dependent = Expression.Parameter(typeof(TDependent), "dependent");

        var body = foreignKey.Properties
            .Zip(foreignKey.PrincipalKey.Properties, (foreign, key) => (Foreign: foreign, Key: key))
            .Select(pair => Expression.Equal(
                Expression.Call(
                    typeof(EF),
                    nameof(EF.Property),
                    [pair.Foreign.ClrType],
                    dependent,
                    Expression.Constant(pair.Foreign.Name)),
                Expression.Convert(
                    Expression.Constant(principal.Property(pair.Key.Name).CurrentValue),
                    pair.Foreign.ClrType)))
            .Aggregate(Expression.AndAlso);

        return Expression.Lambda<Func<TDependent, bool>>(body, dependent);
    }
}
