using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Common.Database;

public static class LetterBucketQueryExtensions
{
    extension<T>(IQueryable<T> source)
    {
        public IQueryable<T> InBucket(Expression<Func<T, string>> label, Option<string> letter) =>
            letter.Match(
                () => source,
                bucket => source.Where(Compose(label, BucketPredicate(bucket))));

        public async Task<IReadOnlyList<string>> LettersAsync(
            Expression<Func<T, string>> label,
            CancellationToken cancellationToken = default)
        {
            var firstCharacters = await source
                .Select(Compose(label, text => text.ToLower().Substring(0, 1)))
                .Distinct()
                .ToListAsync(cancellationToken);

            var present = firstCharacters.Select(first => first.ToLetterBucket()).ToHashSet();

            return LetterBucketExtensions.All.Where(present.Contains).ToList();
        }
    }

    private static Expression<Func<string, bool>> BucketPredicate(string bucket)
    {
        var lowered = bucket.ToLowerInvariant();

        return bucket == LetterBucketExtensions.Other
            ? text => text == ""
                      || text.ToLower().Substring(0, 1).CompareTo("a") < 0
                      || text.ToLower().Substring(0, 1).CompareTo("z") > 0
            : text => text.ToLower().StartsWith(lowered);
    }

    private static Expression<Func<T, TResult>> Compose<T, TResult>(
        Expression<Func<T, string>> label,
        Expression<Func<string, TResult>> projection) =>
        Expression.Lambda<Func<T, TResult>>(
            ReplacingExpressionVisitor.Replace(projection.Parameters[0], label.Body, projection.Body),
            label.Parameters);
}
