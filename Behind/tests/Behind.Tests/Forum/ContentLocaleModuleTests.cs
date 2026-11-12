using System.Security.Claims;
using Behind.Common.Database;
using Behind.Common.WebApi;
using Behind.Common.WebApi.Infrastructure.Localization;
using Behind.Core.Database;
using Behind.Core.WebApi;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Middleware;
using Behind.Forum.Database;
using Behind.Forum.Database.Commands;
using Behind.Forum.Domain;
using Behind.Sport.Database;
using Behind.Sport.Database.Commands;
using Behind.Quotes.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.Forum;

public sealed class ContentLocaleModuleTests : IDisposable
{
    private readonly SportTestBed bed = new();
    private static LocaleCatalog Locales() => new(new ManifestEmbeddedFileProvider(typeof(CoreModule).Assembly, "Frontend"));

    public void Dispose() => bed.Dispose();

    [Theory]
    [InlineData(null, 0)]
    [InlineData("hu-HU", 1)]
    [InlineData("en-US", 0)]
    public async Task ModuleCountsFeedsTopicsAndRepliesInheritTheirLocalizedParents(string? locale, int visible)
    {
        await using var context = bed.Write();
        var category = Assert.IsType<VersionedSaveResult.Saved>(
            await new ForumBoardCategoryCommands(context).Create(new ForumBoardCategory("Localized category"))).Id.Key;
        var board = Assert.IsType<VersionedSaveResult.Saved>(
            await new ForumBoardCommands(context).Create(new ForumBoard("Board", "", null, category))).Id.Key;
        var posts = new ForumPostCommands(context);
        var topic = Assert.IsType<VersionedSaveResult.Saved>(
            await posts.Create(new ForumPost(board, null, Guid.NewGuid(), "Topic", "Body", PublicationState.Published))).Id.Key;
        await posts.Create(new ForumPost(board, topic, Guid.NewGuid(), "", "Reply", PublicationState.Published));
        var discipline = await bed.CreateDisciplineAsync();
        var team = await bed.CreateTeamAsync(discipline);
        var quote = new Quote("Localized quote", "Author");
        context.Quotes.Add(quote);
        var quoteId = Guid.NewGuid();
        context.Entry(quote).StampVersionedKey(quoteId, 1);
        await context.StageContentLocale(nameof(ForumBoardCategory), category, "hu-HU");
        await context.StageContentLocale(nameof(Behind.Sport.Domain.Discipline), discipline, "hu-HU");
        await context.StageContentLocale(nameof(Quote), quoteId, "hu-HU");
        await context.SaveChangesAsync();
        var http = new DefaultHttpContext();
        if (locale is not null)
        {
            http.Request.Headers[ContentLocaleMiddleware.Header] = locale;
        }
        await new ContentLocaleMiddleware(_ => Task.CompletedTask).InvokeAsync(
            http, context, Locales(), Substitute.For<IAuthorizationService>());
        var queries = new ForumQueries(context);
        Assert.Equal(visible, (await queries.Boards(new Flipper(1, 20), DeletionState.Active, default)).Total);
        Assert.Equal(visible * 2, (await queries.LatestPosts(new ModuleCreationOrder(context), 10, default)).Total);
        Assert.Equal(visible, await context.Teams.CountAsync(item => EF.Property<Guid>(item, VersionedColumns.Key) == team));
        Assert.Equal(visible, await context.Quotes.CountAsync());
    }

    [Fact]
    public async Task AssignmentUsesModuleWritePolicyRatherThanAdministration()
    {
        var discipline = await bed.CreateDisciplineAsync();
        await using var context = bed.Write();
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), "Sport.Write")
            .Returns(AuthorizationResult.Success());
        var result = await ContentLocaleEndpoints.Save(
            new(nameof(Behind.Sport.Domain.Discipline), discipline, "hu-HU"),
            new DefaultHttpContext(), context, Locales(), authorization, []);
        Assert.Equal(204, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("hu-HU", await context.FindContentLocale(nameof(Behind.Sport.Domain.Discipline), discipline));
        Assert.Contains(context.Modules.Databases.SelectMany(database => database.ContentLocaleEditPolicies),
            item => item.Key == nameof(Quote) && item.Value == "Quotes.Write");
    }

    [Fact]
    public async Task SportDisciplineSlugCanBeTranslatedBeforeLocaleAssignment()
    {
        await using var context = bed.Write();
        var commands = new DisciplineCommands(context);
        context.ContentLocaleSpecified = true;
        context.SelectedContentLocale = "hu-HU";
        var hungarian = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(new DisciplineDraft("Hungarian", "shared")));
        context.SelectedContentLocale = "en-US";
        var english = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(new DisciplineDraft("English", "shared")));
        Assert.IsType<VersionedSaveResult.Rejected>(await commands.Create(new DisciplineDraft("Duplicate", "shared")));
        Assert.Equal("hu-HU", await context.FindContentLocale(nameof(Behind.Sport.Domain.Discipline), hungarian.Id.Key));
        Assert.Equal("en-US", await context.FindContentLocale(nameof(Behind.Sport.Domain.Discipline), english.Id.Key));
    }
}
