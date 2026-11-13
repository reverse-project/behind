using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

public static class ContentAgeOptionExtensions
{
    extension(AbstractCoreContext context)
    {
        public IQueryable<ContentAgeOption> ActiveContentAgeOptions =>
            context.ContentAgeOptions
                .InDeletionState(context, AbstractCoreContext.DeletionRecorded, DeletionState.Active)
                .LatestOf(context.ContentAgeOptions);

        public async Task<bool> IsActiveContentAgeOption(int age, CancellationToken cancellationToken = default) =>
            age is > 0 and <= ContentAgePolicy.MaximumAge
            && await context.ActiveContentAgeOptions.AnyAsync(item => item.Age == age, cancellationToken);
    }
}
