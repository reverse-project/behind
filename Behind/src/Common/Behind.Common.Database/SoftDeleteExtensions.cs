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
    public const string KeySeparator = ":";

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

        public bool IsRestored =>
            entry.Context is ISoftDeleteContext context && context.Restores.IsMarked(entry.Entity);

        private string SoftDeleteKey =>
            string.Join(
                KeySeparator,
                entry.Metadata.FindPrimaryKey()!.Properties
                    .Select(property => entry.Property(property.Name).CurrentValue!.ToString()!.ToLowerInvariant()));

        public Task<bool> IsDeletedAsync(CancellationToken cancellationToken = default) =>
            entry.Context is ISoftDeleteContext context
                ? context.IsDeletedAsync(entry.Metadata.GetRootType().ClrType.Name, entry.SoftDeleteKey, cancellationToken)
                : Task.FromResult(false);
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

        internal ISoftDeleteContext SoftDeleteContext =>
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

        public async Task RestoreByKeyAsync<TEntity>(
            object[] keyValues,
            CancellationToken cancellationToken = default) where TEntity : class
        {
            using var scope = context.SoftDeleteContext.SoftDelete.IncludeDeleted();
            var entity = await context.Set<TEntity>().FindAsync(keyValues, cancellationToken);

            if (entity is not null)
            {
                await context.RestoreCascadingAsync(context.Entry(entity), cancellationToken);
            }
        }

        private async Task RestoreCascadingAsync(EntityEntry root, CancellationToken cancellationToken)
        {
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var pending = new Queue<EntityEntry>([root]);

            while (pending.TryDequeue(out var principal))
            {
                if (!seen.Add(principal.Entity) || !principal.IsSoftDeletable || !await principal.IsDeletedAsync(cancellationToken))
                {
                    continue;
                }

                context.SoftDeleteContext.Restores.Mark(principal.Entity);

                foreach (var dependent in await SoftDeleteCascade.FindDependentsAsync(context, principal, cancellationToken))
                {
                    pending.Enqueue(dependent);
                }
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

    extension<TEntity>(IQueryable<TEntity> source) where TEntity : class
    {
        public IQueryable<TEntity> InDeletionState<TContext>(
            TContext context,
            Expression<Func<TContext, string, string, bool>> isDeleted,
            DeletionState state)
            where TContext : DbContext, ISoftDeleteContext
        {
            var entityType = context.Model.FindEntityType(typeof(TEntity))?.GetRootType()
                             ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not part of the model.");

            var entity = Expression.Parameter(typeof(TEntity), "entity");
            var recorded = DeletionRecorded(context, isDeleted, entityType, entity);
            var condition = state == DeletionState.Deleted ? recorded : Expression.Not(recorded);

            return source.Where(Expression.Lambda<Func<TEntity, bool>>(condition, entity));
        }
    }

    private static LambdaExpression SoftDeleteFilter<TContext>(
        TContext context,
        Expression<Func<TContext, string, string, bool>> isDeleted,
        IReadOnlyEntityType entityType)
        where TContext : DbContext, ISoftDeleteContext
    {
        var entity = Expression.Parameter(entityType.ClrType, "entity");
        var deletionRecorded = DeletionRecorded(context, isDeleted, entityType, entity);

        var includesDeleted = Expression.Property(
            Expression.Property(Expression.Constant(context), nameof(ISoftDeleteContext.SoftDelete)),
            nameof(SoftDeleteSwitch.IncludesDeleted));

        return Expression.Lambda(Expression.OrElse(includesDeleted, Expression.Not(deletionRecorded)), entity);
    }

    private static Expression DeletionRecorded<TContext>(
        TContext context,
        Expression<Func<TContext, string, string, bool>> isDeleted,
        IReadOnlyEntityType entityType,
        ParameterExpression entity)
        where TContext : DbContext, ISoftDeleteContext =>
        ReplacingExpressionVisitor.Replace(
            isDeleted.Parameters,
            [Expression.Constant(context), Expression.Constant(entityType.ClrType.Name), KeyText(entityType, entity)],
            isDeleted.Body);

    private static Expression KeyText(IReadOnlyEntityType entityType, Expression entity)
    {
        var keys = entityType.FindPrimaryKey()?.Properties
                   ?? throw new NotSupportedException($"{entityType.ClrType.Name} must have a primary key to be soft deletable.");

        return keys
            .Select(key => LowerText(entity, key))
            .Aggregate((left, right) => Concat(Concat(left, Expression.Constant(KeySeparator)), right));
    }

    private static Expression LowerText(Expression entity, IReadOnlyProperty key) =>
        Expression.Call(
            Expression.Call(
                Expression.Call(typeof(EF), nameof(EF.Property), [key.ClrType], entity, Expression.Constant(key.Name)),
                nameof(ToString),
                Type.EmptyTypes),
            nameof(string.ToLower),
            Type.EmptyTypes);

    private static Expression Concat(Expression left, Expression right) =>
        Expression.Call(
            typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!,
            left,
            right);
}
