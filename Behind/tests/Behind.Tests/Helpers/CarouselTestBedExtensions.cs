using Behind.Core.Database;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;

namespace Behind.Tests.Helpers;

public static class CarouselTestBedExtensions
{
    extension(NewsTestBed bed)
    {
        public async Task<TResult> WithCarouselCommandsAsync<TResult>(
            Func<CarouselCommands, Task<TResult>> action)
        {
            await using var ctx = bed.Write();
            await using var versions = bed.WriteVersions();

            return await action(new CarouselCommands(ctx, versions, new VfsCommands(ctx, bed.ContentStore)));
        }

        public async Task<TResult> WithCarouselQueriesAsync<TResult>(
            Func<CarouselQueries, Task<TResult>> action)
        {
            await using var ctx = bed.Read();

            return await action(new CarouselQueries(ctx));
        }

        public async Task<TResult> WithSiteCommandsAsync<TResult>(Func<SiteCommands, Task<TResult>> action)
        {
            await using var ctx = bed.Write();
            await using var versions = bed.WriteVersions();
            var files = new VfsCommands(ctx, bed.ContentStore);

            return await action(new SiteCommands(
                ctx,
                versions,
                new FixedTimeProvider(NewsTestBed.Now),
                files,
                new CarouselCommands(ctx, versions, files)));
        }

        public async Task<Guid> CreateSiteAsync(string title = "About", string slug = "about", bool publish = true)
        {
            var result = await bed.WithSiteCommandsAsync(commands => commands.Create(title, slug, "<p/>", publish));

            return Assert.IsType<SiteSaveResult.Saved>(result).Id.Key;
        }

        public Task<Guid> AddCarouselImageAsync(string fileName = "slide.png") =>
            bed.AddVfsFileAsync(VfsTypes.Carousels, fileName);

        public async Task<ICarouselCommands.CarouselDraft> NewCarouselDraftAsync(
            int intervalSeconds = 5,
            params string[] captions)
        {
            var slides = new List<ICarouselCommands.CarouselSlideDraft>();

            foreach (var caption in captions.Length == 0 ? ["First", "Second"] : captions)
            {
                slides.Add(new ICarouselCommands.CarouselSlideDraft(await bed.AddCarouselImageAsync(), caption, string.Empty));
            }

            return new ICarouselCommands.CarouselDraft(intervalSeconds, slides);
        }

        public async Task<VersionedKey<Guid>> SaveCarouselAsync(
            Guid siteId,
            ICarouselCommands.CarouselDraft? draft = null)
        {
            var toSave = draft ?? await bed.NewCarouselDraftAsync();

            var result = await bed.WithCarouselCommandsAsync(commands => commands.Save(siteId, toSave));

            return Assert.IsType<CarouselSaveResult.Saved>(result).Id;
        }
    }
}
