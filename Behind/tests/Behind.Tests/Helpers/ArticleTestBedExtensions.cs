using Behind.Core.Database;
using Behind.Core.Database.Commands;

namespace Behind.Tests.Helpers;

public static class ArticleTestBedExtensions
{
    extension(NewsTestBed bed)
    {
        public async Task<TResult> WithArticleCommandsAsync<TResult>(
            Func<ArticleCommands, Task<TResult>> action,
            DateTimeOffset? at = null)
        {
            await using var ctx = bed.Write(at);
            await using var versions = bed.WriteVersions(at);

            return await action(new ArticleCommands(ctx, versions, new VfsCommands(ctx, bed.ContentStore)));
        }

        public async Task<VersionedKey<Guid>> SaveArticleAsync(
            Guid? key,
            string title = "Guide",
            Option<Guid> categoryId = default,
            DateTimeOffset? at = null,
            Guid? authorId = null,
            string summary = "<p>short</p>",
            string content = "<p>long</p>",
            bool publish = true)
        {
            var draft = new IArticleCommands.ArticleDraft(title, summary, content, categoryId);

            var result = await bed.WithArticleCommandsAsync(
                commands => key is { } existing
                    ? commands.SaveVersion(existing, draft, publish)
                    : commands.Create(authorId ?? bed.AuthorId, draft, publish),
                at);

            return Assert.IsType<ArticleSaveResult.Saved>(result).Id;
        }

        public async Task<Guid> CreateArticleAsync(
            string title = "Guide",
            Option<Guid> categoryId = default,
            DateTimeOffset? at = null,
            Guid? authorId = null,
            string summary = "<p>short</p>",
            string content = "<p>long</p>",
            bool publish = true) =>
            (await bed.SaveArticleAsync(null, title, categoryId, at, authorId, summary, content, publish)).Key;

        public async Task<Guid> CreateArticleCategoryAsync(
            string name = "Tutorials",
            Option<Guid> headerImageId = default)
        {
            await using var ctx = bed.Write();
            var result = await new ArticleCategoryCommands(ctx, new VfsCommands(ctx, bed.ContentStore)).Create(
                new IArticleCategoryCommands.ArticleCategoryDraft(name, headerImageId));

            return Assert.IsType<ArticleCategorySaveResult.Saved>(result).Id.Key;
        }
    }
}
