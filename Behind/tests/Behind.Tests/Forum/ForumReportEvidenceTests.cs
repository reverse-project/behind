using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Forum.Common;
using Behind.Forum.Database;
using Behind.Forum.Database.Commands;
using Behind.Forum.Domain;
using Behind.Forum.WebApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Behind.Tests.Forum;

public sealed class ForumReportEvidenceTests : IDisposable
{
    private readonly SportTestBed _bed = new();

    public void Dispose() => _bed.Dispose();

    [Theory]
    [InlineData(PublicationState.Published, true, true)]
    [InlineData(PublicationState.Pending, false, true)]
    [InlineData(PublicationState.Rejected, false, true)]
    [InlineData(PublicationState.Published, false, false)]
    public async Task EvidenceUsesPublicPublicationAndParentVisibility(PublicationState state, bool visible, bool enabled)
    {
        await using var context = _bed.Write();
        var board = Assert.IsType<VersionedSaveResult.Saved>(
            await new ForumBoardCommands(context).Create(new ForumBoard("Board", "", null))).Id.Key;
        var author = Guid.NewGuid();
        var post = Assert.IsType<VersionedSaveResult.Saved>(await new ForumPostCommands(context).Create(
            new ForumPost(board, null, author, "Topic", "[b]Evidence[/b]", state, true))).Id.Key;
        var source = new ForumReportEvidenceSource(new ForumQueries(context), new FakeAuthorizationService(ForumNames.Read),
            new ModuleAvailability(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Backend:Modules:forum"] = enabled.ToString() }).Build()));
        var evidence = await source.Find(new DefaultHttpContext(), post, default);
        Assert.Equal(visible, evidence is not null);
        if (evidence is not null)
        {
            Assert.Equal(author, evidence.AuthorId);
            Assert.Equal("[b]Evidence[/b]", evidence.Content);
        }

        context.HiddenContentIds = [board];
        Assert.Null(await source.Find(new DefaultHttpContext(), post, default));
    }

    [Fact]
    public async Task ForumReadGrantIsRequiredEvenForAdministratorsWithReportAccess()
    {
        await using var context = _bed.Write();
        var board = Assert.IsType<VersionedSaveResult.Saved>(
            await new ForumBoardCommands(context).Create(new ForumBoard("Board", "", null))).Id.Key;
        var post = Assert.IsType<VersionedSaveResult.Saved>(await new ForumPostCommands(context).Create(
            new ForumPost(board, null, Guid.NewGuid(), "Topic", "Evidence", PublicationState.Published))).Id.Key;
        var source = new ForumReportEvidenceSource(new ForumQueries(context), new FakeAuthorizationService("Report.Manage"),
            new ModuleAvailability(new ConfigurationBuilder().Build()));
        Assert.Null(await source.Find(new DefaultHttpContext(), post, default));
    }
}
