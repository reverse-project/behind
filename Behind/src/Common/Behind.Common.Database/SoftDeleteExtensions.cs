using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace Behind.Common.Database;

public static class SoftDeleteExtensions
{
    public const string SoftDeletableAnnotation = "Behind:SoftDeletable";
    public const string FilterName = "SoftDelete";

    extension(ModelBuilder modelBuilder)
    {
        public ModelBuilder ApplySoftDelete<TContext>(
            TContext context,
            Expression<Func<TContext, string, string, bool>> isDeleted,
            params Type[] rootEntityTypes)
            where TContext : DbContext, ISoftDeleteContext
        {
            foreach (var entityType in rootEntityTypes)
            {
                var builder = modelBuilder.Entity(entityType);
                builder.HasAnnotation(SoftDeletableAnnotation, true);
                builder.HasQueryFilter(FilterName, SoftDeleteFilter(context, isDeleted, builder.Metadata));
            }

            return modelBuilder;
        }
    }

    extension(IReadOnlyEntityType entityType)
    {
        public bool IsSoftDeletable => entityType.GetRootType().FindAnnotation(SoftDeletableAnnotation) is not null;
    }

    extension(EntityEntry entry)
    {
        public bool IsSoftDeletable => entry.Metadata.IsSoftDeletable;

        public bool IsHardDeleted =>
            entry.Context is ISoftDeleteContext context && context.HardDeletes.IsMarked(entry.Entity);
    }

    extension(DbContext context)
    {
        public void CancelSoftDeletes()
        {
            var softDeleted = context.ChangeTracker.Entries()
                .Where(entry => entry.State == EntityState.Deleted && entry.IsSoftDeletable && !entry.IsHardDeleted)
                .ToList();

            foreach (var entry in softDeleted)
            {
                entry.State = EntityState.Unchanged;
            }
        }

        private ISoftDeleteContext SoftDeleteContext =>
            context as ISoftDeleteContext
            ?? throw new InvalidOperationException($"{context.GetType().Name} does not support soft delete.");

        public void HardDelete<TEntity>(TEntity entity) where TEntity : class
        {
            context.SoftDeleteContext.HardDeletes.Mark(entity);
            context.Remove(entity);
        }

        public async Task HardDeleteByKeyAsync<TEntity>(
            object[] keyValues,
            CancellationToken cancellationToken = default) where TEntity : class
        {
            using var scope = context.SoftDeleteContext.SoftDelete.IncludeDeleted();
            var entity = await context.Set<TEntity>().FindAsync(keyValues, cancellationToken);

            if (entity is not null)
            {
                context.HardDelete(entity);
            }
        }

        public async Task ExpandCascadingDeletesAsync(CancellationToken cancellationToken = default)
        {
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var pending = new Queue<EntityEntry>(
                context.ChangeTracker.Entries().Where(entry => entry.State == EntityState.Deleted)
            );

            while (pending.TryDequeue(out var principal))
            {
                if (!seen.Add(principal.Entity))
                {
                    continue;
                }

                var hard = principal.IsHardDeleted;
                using var scope = hard ? context.SoftDeleteContext.SoftDelete.IncludeDeleted() : null;

                foreach (var dependent in await SoftDeleteCascade.FindDependentsAsync(context, principal, cancellationToken))
                {
                    if (hard)
                    {
                        context.SoftDeleteContext.HardDeletes.Mark(dependent.Entity);
                    }

                    dependent.State = EntityState.Deleted;
                    pending.Enqueue(dependent);
                }
            }
        }
    }

    private static LambdaExpression SoftDeleteFilter<TContext>(
        TContext context,
        Expression<Func<TContext, string, string, bool>> isDeleted,
        IReadOnlyEntityType entityType)
        where TContext : DbContext, ISoftDeleteContext
    {
        var key = entityType.FindPrimaryKey()?.Properties is [var single]
            ? single
            : throw new NotSupportedException($"{entityType.ClrType.Name} must have a single property key to be soft deletable.");

        var entity = Expression.Parameter(entityType.ClrType, "entity");
        var keyText = Expression.Call(
            Expression.Call(
                Expression.Call(typeof(EF), nameof(EF.Property), [key.ClrType], entity, Expression.Constant(key.Name)),
                nameof(ToString),
                Type.EmptyTypes),
            nameof(string.ToLower),
            Type.EmptyTypes);

        var deletionRecorded = ReplacingExpressionVisitor.Replace(
            isDeleted.Parameters,
            [Expression.Constant(context), Expression.Constant(entityType.ClrType.Name), keyText],
            isDeleted.Body);

        var includesDeleted = Expression.Property(
            Expression.Property(Expression.Constant(context), nameof(ISoftDeleteContext.SoftDelete)),
            nameof(SoftDeleteSwitch.IncludesDeleted));

        return Expression.Lambda(Expression.OrElse(includesDeleted, Expression.Not(deletionRecorded)), entity);
    }
}
