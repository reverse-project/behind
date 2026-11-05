using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database.Commands;

public sealed class CarouselCommands(
    AbstractCoreContext context,
    AbstractVersioningContext versions,
    IVfsCommands files) : ICarouselCommands
{
    public async Task<CarouselSaveResult> Save(
        Guid siteId,
        ICarouselCommands.CarouselDraft draft,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Sites.OfSite(siteId).AnyAsync(cancellationToken))
        {
            return new CarouselSaveResult.NotFound();
        }

        if (!await HasImages(draft, cancellationToken))
        {
            return new CarouselSaveResult.UnknownImage();
        }

        var id = new VersionedKey<Guid>(siteId, await context.LatestCarouselVersionAsync(siteId, cancellationToken) + 1);
        var carousel = new Carousel(draft.IntervalSeconds);

        await context.Carousels.AddAsync(carousel, cancellationToken);
        context.Entry(carousel).StampVersionedKey(id.Key, id.Version);

        foreach (var (slide, position) in draft.Slides.Select((slide, position) => (slide, position)))
        {
            await AddSlide(id, slide, position, cancellationToken);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return new CarouselSaveResult.VersionConflict();
        }

        await files.Attach(VfsTypes.Carousels, siteId, ImageKeysOf(draft), cancellationToken);

        await versions.SetActiveVersionAsync(Carousel.EntityType, siteId.ToKey(), id.Version, cancellationToken);
        await versions.SaveChangesAsync(cancellationToken);

        return new CarouselSaveResult.Saved(id);
    }

    public async Task<bool> Delete(Guid siteId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.MarkSoftDeletedAsync(context.Carousels.OfKey(siteId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return deleted > 0;
    }

    public async Task<bool> Restore(Guid siteId, CancellationToken cancellationToken = default)
    {
        var restored = await context.MarkRestoredAsync(context.Carousels.OfKey(siteId), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return restored > 0;
    }

    public async Task<HardDeleteResult> HardDelete(Guid siteId, CancellationToken cancellationToken = default)
    {
        var deleted = await context.DeletedRowsAsync(context.Carousels.OfKey(siteId), cancellationToken);

        if (deleted.Count == 0)
        {
            return HardDeleteResult.NotFound;
        }

        var deletedVersions = deleted
            .Select(row => context.Entry(row).Property<long>(VersionedColumns.Version).CurrentValue)
            .ToList();

        var slides = await context.CarouselSlides
            .Where(slide => EF.Property<Guid>(slide, CarouselSlideConfiguration.CarouselIdProperty) == siteId
                            && deletedVersions.Contains(EF.Property<long>(slide, VersionedColumns.Version)))
            .ToListAsync(cancellationToken);

        var imageIds = slides
            .Select(slide => context.Entry(slide).Property<Guid>(CarouselSlideConfiguration.ImageIdProperty).CurrentValue)
            .Distinct()
            .ToList();

        deleted.ForEach(context.HardDelete);
        context.CarouselSlides.RemoveRange(slides);

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            if (!await context.Carousels.OfKey(siteId).AnyAsync(cancellationToken))
            {
                await versions.ClearActiveVersionAsync(Carousel.EntityType, siteId.ToKey(), cancellationToken);
                await versions.SaveChangesAsync(cancellationToken);
            }

            await files.Delete(await UnusedImages(imageIds, cancellationToken), cancellationToken);

            return HardDeleteResult.Deleted;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();

            return HardDeleteResult.Rejected;
        }
    }

    private async Task AddSlide(
        VersionedKey<Guid> carousel,
        ICarouselCommands.CarouselSlideDraft draft,
        int position,
        CancellationToken cancellationToken)
    {
        var slide = new CarouselSlide(draft.Caption, draft.LinkUrl);

        await context.CarouselSlides.AddAsync(slide, cancellationToken);

        var entry = context.Entry(slide);
        entry.StampVersionedKey(Guid.CreateVersion7(), carousel.Version);
        entry.Property<Guid>(CarouselSlideConfiguration.CarouselIdProperty).CurrentValue = carousel.Key;
        entry.Property<Guid>(CarouselSlideConfiguration.ImageIdProperty).CurrentValue = draft.ImageId;
        entry.Property<int>(CarouselSlideConfiguration.PositionProperty).CurrentValue = position;
    }

    private static IReadOnlyList<Guid> ImageKeysOf(ICarouselCommands.CarouselDraft draft) =>
        draft.Slides.Select(slide => slide.ImageId).Distinct().ToList();

    private async Task<bool> HasImages(ICarouselCommands.CarouselDraft draft, CancellationToken cancellationToken)
    {
        var wanted = ImageKeysOf(draft);

        var found = await context.VfsFiles
            .InSameKindAs(VfsTypes.Carousels)
            .OfKeys(wanted)
            .Select(file => EF.Property<Guid>(file, VersionedColumns.Key))
            .Distinct()
            .CountAsync(cancellationToken);

        return found == wanted.Count;
    }

    private async Task<IReadOnlyList<Guid>> UnusedImages(
        IReadOnlyList<Guid> candidates,
        CancellationToken cancellationToken)
    {
        var used = await context.CarouselSlides
            .Where(slide => candidates.Contains(EF.Property<Guid>(slide, CarouselSlideConfiguration.ImageIdProperty)))
            .Select(slide => EF.Property<Guid>(slide, CarouselSlideConfiguration.ImageIdProperty))
            .Distinct()
            .ToListAsync(cancellationToken);

        return candidates.Except(used).ToList();
    }
}
