using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Aggregates;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Auditing;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Middleware;
using Behind.Common.Database.Raw;
using Behind.Common.WebApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Behind.Tests.CoreWebApi;

public sealed class ContentRestrictionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
    private readonly TestDbContextFactory _factory = new();
    private readonly IOptionsMonitor<ContentRestrictionsConfiguration> _configuration =
        Substitute.For<IOptionsMonitor<ContentRestrictionsConfiguration>>();

    public void Dispose() => _factory.Dispose();

    private static IAuthorizationService Authorization(params string[] grants)
    {
        var authorization = Substitute.For<IAuthorizationService>();
        authorization.AuthorizeAsync(Arg.Any<ClaimsPrincipal>(), Arg.Any<object?>(), Arg.Any<string>())
            .Returns(call => grants.Contains(call.ArgAt<string>(2)) ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        return authorization;
    }

    private async Task<(Guid User, Guid Category, Guid Restricted, Guid Public)> Seed(DateOnly? birthday = null)
    {
        await using var context = _factory.CreateContext();
        var user = new HumanUser("Author", [new Email("author@test.example")], None) { Birthday = birthday };
        context.Users.Add(user);
        var userId = context.Entry(user).Property<Guid>("Id").CurrentValue;
        var category = new NewsCategory("Adults");
        context.NewsCategories.Add(category);
        context.Entry(category).StampVersionedKey(Guid.NewGuid(), 1);
        var categoryId = context.Entry(category).Property<Guid>(VersionedColumns.Key).CurrentValue;
        var restricted = AddNews("Restricted", categoryId);
        var unrestricted = AddNews("Public", null);
        var comment = new NewsComment(new VersionedKey<Guid>(Guid.NewGuid(), 1), "Secret comment");
        context.NewsComments.Add(comment);
        context.Entry(comment).StampVersionedKey(comment.Id.Key, 1);
        context.Entry(comment).Property<Guid>(NewsCommentConfiguration.NewsIdProperty).CurrentValue = restricted;
        context.Entry(comment).Property<Guid>(NewsCommentConfiguration.AuthorIdProperty).CurrentValue = userId;
        var file = new VfsFile(VfsTypes.News, "private.png", "image/png", 1, restricted);
        context.VfsFiles.Add(file);
        context.Entry(file).StampVersionedKey(Guid.NewGuid(), 1);
        var restriction = new ContentRestriction(nameof(NewsCategory), categoryId, 18);
        context.ContentRestrictions.Add(restriction);
        context.Entry(restriction).StampVersionedKey(Guid.NewGuid(), 1);
        await context.SaveChangesAsync();
        await using var versions = _factory.CreateVersioningContext();
        versions.ActiveVersions.AddRange(
            new ActiveVersion(News.EntityType, restricted.ToString(), 1),
            new ActiveVersion(News.EntityType, unrestricted.ToString(), 1));
        await versions.SaveChangesAsync();
        return (userId, categoryId, restricted, unrestricted);

        Guid AddNews(string title, Guid? categoryKey)
        {
            var news = new News(title, "Summary", "Content", false);
            context.NewsEntries.Add(news);
            context.Entry(news).StampVersionedKey(Guid.NewGuid(), 1);
            context.Entry(news).Property<Guid>(NewsConfiguration.AuthorIdProperty).CurrentValue = userId;
            context.Entry(news).Property<Guid?>(NewsConfiguration.CategoryIdProperty).CurrentValue = categoryKey;
            return context.Entry(news).Property<Guid>(VersionedColumns.Key).CurrentValue;
        }
    }

    private async Task Invoke(AbstractCoreContext context, HttpContext http, ISessionQueries? sessions = null)
    {
        var middleware = new ContentRestrictionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(http, context, sessions ?? Substitute.For<ISessionQueries>(),
            new FixedTimeProvider(Now), _configuration);
    }

    [Fact]
    public async Task DisabledByDefault_DoesNotResolveIdentityOrFilterContent()
    {
        var ids = await Seed();
        _configuration.CurrentValue.Returns(new ContentRestrictionsConfiguration());
        var sessions = Substitute.For<ISessionQueries>();
        await using var context = _factory.CreateContext();
        await Invoke(context, new DefaultHttpContext(), sessions);

        Assert.Empty(context.HiddenContentIds);
        Assert.Equal(2, await context.NewsEntries.CountAsync());
        Assert.Single(await context.NewsComments.ToListAsync());
        Assert.Single(await context.VfsFiles.ToListAsync());
        Assert.Empty(sessions.ReceivedCalls());
        Assert.False(new ContentRestrictionsConfiguration().Enabled);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("2008-11-13", false)]
    [InlineData("2008-11-12", true)]
    [InlineData("2008-11-11", true)]
    public async Task Enabled_EnforcesListsDetailsSearchCountsWidgetsCommentsAndMedia(string? birthday, bool allowed)
    {
        var ids = await Seed(birthday is null ? null : DateOnly.Parse(birthday));
        _configuration.CurrentValue.Returns(new ContentRestrictionsConfiguration { Enabled = true });
        var session = Guid.NewGuid();
        var http = SessionTestHelper.SignedIn(session);
        await using var context = _factory.CreateContext();
        await Invoke(context, http, SessionTestHelper.SessionQueries(session, ids.User));

        var queries = new NewsQueries(context);
        var page = await queries.List(new Flipper(1, 10), None, None, Now);
        Assert.Equal(allowed ? 2 : 1, page.Total);
        Assert.Equal(allowed ? 2 : 1, (await queries.Latest(10, Now)).Count);
        Assert.Equal(allowed, (await queries.Find(ids.Restricted, Now)) != None);
        Assert.Equal(allowed, (await queries.FindVersion(new VersionedKey<Guid>(ids.Restricted, 1))) != None);
        Assert.Equal(allowed ? 1 : 0, await context.NewsEntries.CountAsync(news => news.Title.Contains("Restricted")));
        Assert.Equal(allowed ? 1 : 0, await context.NewsComments.CountAsync());
        Assert.Equal(allowed ? 1 : 0, await context.VfsFiles.CountAsync());
        Assert.Equal(allowed ? 1 : 0, await context.NewsCategories.CountAsync());
        Assert.Equal("private, no-store", http.Response.Headers.CacheControl);
    }

    [Fact]
    public async Task Enabled_AnonymousVisitorCannotViewRestrictedContent()
    {
        await Seed();
        _configuration.CurrentValue.Returns(new ContentRestrictionsConfiguration { Enabled = true });
        await using var context = _factory.CreateContext();
        await Invoke(context, new DefaultHttpContext());

        Assert.Equal("Public", Assert.Single(await context.NewsEntries.ToListAsync()).Title);
        Assert.Empty(await context.NewsComments.ToListAsync());
    }

    [Fact]
    public async Task Reload_DisablingClearsRestrictionsOnTheNextRequest()
    {
        await Seed();
        var current = new ContentRestrictionsConfiguration { Enabled = true };
        _configuration.CurrentValue.Returns(_ => current);
        await using var context = _factory.CreateContext();
        await Invoke(context, new DefaultHttpContext());
        Assert.Equal(1, await context.NewsEntries.CountAsync());

        current = current with { Enabled = false };
        await Invoke(context, new DefaultHttpContext());

        Assert.Equal(2, await context.NewsEntries.CountAsync());
        Assert.Empty(context.HiddenContentIds);
    }

    [Fact]
    public async Task RestrictedGalleryItem_AlsoBlocksItsOriginalAndThumbnailEvenWhenAttachedToTheGallery()
    {
        await using var context = _factory.CreateContext();
        var author = await DatabaseSeedHelper.SeedHumanUserAsync(context);
        var gallery = new Gallery(new VersionedKey<Guid>(Guid.NewGuid(), 1), "Gallery", "");
        context.Galleries.Add(gallery);
        context.Entry(gallery).StampVersionedKey(gallery.Id.Key, 1);
        context.Entry(gallery).Property<Guid>(GalleryConfiguration.AuthorIdProperty).CurrentValue = author;
        var original = new VfsFile(VfsTypes.Galleries, "original.jpg", "image/jpeg", 1, gallery.Id.Key);
        var thumbnail = new VfsFile(VfsTypes.Galleries, "thumbnail.jpg", "image/jpeg", 1, gallery.Id.Key);
        context.VfsFiles.Add(original);
        var originalId = Guid.NewGuid();
        context.Entry(original).StampVersionedKey(originalId, 1);
        context.VfsFiles.Add(thumbnail);
        var thumbnailId = Guid.NewGuid();
        context.Entry(thumbnail).StampVersionedKey(thumbnailId, 1);
        var item = new GalleryItem("Restricted image");
        context.GalleryItems.Add(item);
        var itemId = Guid.NewGuid();
        context.Entry(item).StampVersionedKey(itemId, 1);
        context.Entry(item).Property<Guid>(GalleryItemConfiguration.GalleryIdProperty).CurrentValue = gallery.Id.Key;
        context.Entry(item).Property<Guid>(GalleryItemConfiguration.ImageIdProperty).CurrentValue = originalId;
        context.Entry(item).Property<Guid>(GalleryItemConfiguration.ThumbnailIdProperty).CurrentValue = thumbnailId;
        var restriction = new ContentRestriction(nameof(GalleryItem), itemId, 18);
        context.ContentRestrictions.Add(restriction);
        context.Entry(restriction).StampVersionedKey(Guid.NewGuid(), 1);
        await context.SaveChangesAsync();
        _configuration.CurrentValue.Returns(new ContentRestrictionsConfiguration { Enabled = true });

        await Invoke(context, new DefaultHttpContext());

        Assert.Empty(await context.GalleryItems.ToListAsync());
        Assert.Empty(await context.VfsFiles.ToListAsync());
        Assert.Single(await context.Galleries.ToListAsync());
    }

    [Fact]
    public async Task Restrictions_HaveEditableRawColumnsAndHumanReadableAuditLabels()
    {
        await Seed();
        await using var context = _factory.CreateContext();
        var table = Assert.Single(context.RawTables, table => table.EntityName == nameof(ContentRestriction));
        Assert.False(context.IsReadOnly(table));
        var id = await context.ContentRestrictions.Select(item => EF.Property<Guid>(item, VersionedColumns.Key)).SingleAsync();
        var labels = await context.ResolveAuditLabelsAsync([id], []);

        Assert.Equal("NewsCategory minimum age 18", labels.Label(id.ToString()));
    }

    [Fact]
    public async Task Management_SupportsSoftDeleteRestoreAndDeletedOnlyHardDelete()
    {
        var ids = await Seed();
        await using var context = _factory.CreateAuditedContext(
            AuditActor.ForUser(ids.User), new FixedTimeProvider(Now));
        var id = await context.ContentRestrictions.Select(item => EF.Property<Guid>(item, VersionedColumns.Key)).SingleAsync();
        var http = new DefaultHttpContext();

        Assert.IsType<NotFound>(await ContentRestrictionEndpoints.HardDelete(id, http, context));
        Assert.IsType<NoContent>(await ContentRestrictionEndpoints.Delete(id, http, context));
        Assert.Empty(await context.ContentRestrictions.ToListAsync());
        Assert.IsType<NoContent>(await ContentRestrictionEndpoints.Restore(id, http, context));
        Assert.Single(await context.ContentRestrictions.ToListAsync());
        Assert.IsType<NoContent>(await ContentRestrictionEndpoints.Delete(id, http, context));
        Assert.IsType<NoContent>(await ContentRestrictionEndpoints.HardDelete(id, http, context));
        using var scope = context.SoftDelete.IncludeDeleted();
        Assert.Empty(await context.ContentRestrictions.ToListAsync());
    }

    [Fact]
    public async Task Save_PersistsNewVersionsAndZeroRemovesTheAgeLimit()
    {
        var ids = await Seed();
        await using var context = _factory.CreateContext();
        var http = new DefaultHttpContext();
        Assert.IsType<Created<ContentAgeOptionEndpoints.AgeOptionResponse>>(
            await ContentAgeOptionEndpoints.Create(new(21, "21"), http, context));
        Assert.IsType<NoContent>(await ContentRestrictionEndpoints.Save(
            new(nameof(NewsCategory), ids.Category, 21), http, context, Authorization("ContentRestriction.Write", "News.Write"), []));
        Assert.IsType<NoContent>(await ContentRestrictionEndpoints.Save(
            new(nameof(NewsCategory), ids.Category, 0), http, context, Authorization("ContentRestriction.Write", "News.Write"), []));

        var rows = await context.ContentRestrictions.OrderBy(item => EF.Property<long>(item, VersionedColumns.Version))
            .Select(item => item.MinimumAge).ToListAsync();
        Assert.Equal([18, 21, 0], rows);
        _configuration.CurrentValue.Returns(new ContentRestrictionsConfiguration { Enabled = true });
        await Invoke(context, new DefaultHttpContext());
        Assert.Equal(2, await context.NewsEntries.CountAsync());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(151)]
    public async Task Save_RejectsInvalidAges(int age)
    {
        await using var context = _factory.CreateContext();
        Assert.IsType<BadRequest>(await ContentRestrictionEndpoints.Save(
            new(nameof(News), Guid.NewGuid(), age), new DefaultHttpContext(), context, Authorization(), []));
        Assert.Empty(await context.ContentRestrictions.ToListAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Save_RequiresRestrictionAndEntityGrants(bool restrictionGrant, bool entityGrant)
    {
        var ids = await Seed();
        await using var context = _factory.CreateContext();
        var grants = new List<string>();
        if (restrictionGrant)
        {
            grants.Add("ContentRestriction.Write");
        }
        if (entityGrant)
        {
            grants.Add("News.Write");
        }
        var result = await ContentRestrictionEndpoints.Save(
            new(nameof(NewsCategory), ids.Category, 0), new DefaultHttpContext(), context, Authorization([..grants]), []);
        Assert.Equal(403, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Single(await context.ContentRestrictions.ToListAsync());
    }

    [Fact]
    public async Task Save_RejectsMissingEntity()
    {
        await using var context = _factory.CreateContext();
        var result = await ContentRestrictionEndpoints.Save(
            new(nameof(NewsCategory), Guid.NewGuid(), 0), new DefaultHttpContext(), context,
            Authorization("ContentRestriction.Write", "News.Write"), []);
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task Save_RejectsAgesOutsideTheActiveOptions()
    {
        var ids = await Seed();
        await using var context = _factory.CreateContext();
        var result = await ContentRestrictionEndpoints.Save(
            new(nameof(NewsCategory), ids.Category, 21), new DefaultHttpContext(), context,
            Authorization("ContentRestriction.Write", "News.Write"), []);
        Assert.IsType<BadRequest>(result);
        Assert.Single(await context.ContentRestrictions.ToListAsync());
    }

    [Fact]
    public async Task RemovedAgeRemainsEnforcedAndCanOnlyBeRetainedByItsExistingEntity()
    {
        var ids = await Seed();
        var http = new DefaultHttpContext();
        var authorization = Authorization("ContentRestriction.Write", "News.Write");
        Guid optionId;
        await using (var context = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            optionId = Assert.IsType<Created<ContentAgeOptionEndpoints.AgeOptionResponse>>(
                await ContentAgeOptionEndpoints.Create(new(18, "Adult"), http, context)).Value!.Id;
            Assert.IsType<Created<ContentAgeOptionEndpoints.AgeOptionResponse>>(
                await ContentAgeOptionEndpoints.Create(new(12, "Child"), http, context));
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(optionId, http, context));
        }

        _configuration.CurrentValue.Returns(new ContentRestrictionsConfiguration { Enabled = true });
        await using (var context = _factory.CreateContext())
        {
            await Invoke(context, new DefaultHttpContext());
            Assert.Equal("Public", Assert.Single(await context.NewsEntries.ToListAsync()).Title);
        }

        await using (var context = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            Assert.IsType<NoContent>(await ContentRestrictionEndpoints.Save(
                new(nameof(NewsCategory), ids.Category, 18), http, context, authorization, []));
            Assert.IsType<BadRequest>(await ContentRestrictionEndpoints.Save(
                new(nameof(News), ids.Public, 18), http, context, authorization, []));
            Assert.IsType<NoContent>(await ContentRestrictionEndpoints.Save(
                new(nameof(NewsCategory), ids.Category, 12), http, context, authorization, []));
            Assert.IsType<BadRequest>(await ContentRestrictionEndpoints.Save(
                new(nameof(NewsCategory), ids.Category, 18), http, context, authorization, []));
            Assert.IsType<NoContent>(await ContentRestrictionEndpoints.Save(
                new(nameof(NewsCategory), ids.Category, 0), http, context, authorization, []));
            var ages = await context.ContentRestrictions
                .OrderBy(item => EF.Property<long>(item, VersionedColumns.Version))
                .Select(item => item.MinimumAge).ToArrayAsync();
            Assert.Equal([18, 18, 12, 0], ages);
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.HardDelete(optionId, http, context));
        }
    }

    [Fact]
    public async Task HardDeletedOptionStillEnforcesItsStoredNumericAge()
    {
        var ids = await Seed();
        await using (var context = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            var http = new DefaultHttpContext();
            var id = Assert.IsType<Created<ContentAgeOptionEndpoints.AgeOptionResponse>>(
                await ContentAgeOptionEndpoints.Create(new(18, "Adult"), http, context)).Value!.Id;
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.Delete(id, http, context));
            Assert.IsType<NoContent>(await ContentAgeOptionEndpoints.HardDelete(id, http, context));
            Assert.IsType<NoContent>(await ContentRestrictionEndpoints.Save(
                new(nameof(NewsCategory), ids.Category, 18), http, context,
                Authorization("ContentRestriction.Write", "News.Write"), []));
        }
        _configuration.CurrentValue.Returns(new ContentRestrictionsConfiguration { Enabled = true });
        await using var read = _factory.CreateContext();
        await Invoke(read, new DefaultHttpContext());
        Assert.Equal("Public", Assert.Single(await read.NewsEntries.ToListAsync()).Title);
    }

    [Theory]
    [InlineData(false, 403)]
    [InlineData(true, 204)]
    public async Task Save_EnforcesOwnershipSensitiveEntityPermission(bool ownerAllowed, int status)
    {
        var ids = await Seed();
        await using var context = _factory.CreateContext();
        var permission = Substitute.For<IContentLocalePermission>();
        permission.EntityType.Returns(nameof(NewsCategory));
        permission.CanEdit(Arg.Any<HttpContext>(), ids.Category).Returns(ownerAllowed);
        var result = await ContentRestrictionEndpoints.Save(
            new(nameof(NewsCategory), ids.Category, 0), new DefaultHttpContext(), context,
            Authorization("ContentRestriction.Write", "News.Write"), [permission]);
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(false, false, false, false, 403)]
    [InlineData(true, false, false, false, 200)]
    [InlineData(true, false, false, true, 403)]
    [InlineData(false, true, false, true, 403)]
    [InlineData(false, true, true, true, 200)]
    public async Task AgeOptionListing_RequiresTheAppropriateGrant(
        bool canEdit, bool canManage, bool canViewDeleted, bool deleted, int status)
    {
        await using var context = _factory.CreateContext();
        var grants = new List<string>();
        if (canEdit)
        {
            grants.Add("ContentRestriction.Write");
        }
        if (canManage)
        {
            grants.Add("ContentAge.Manage");
        }
        if (canViewDeleted)
        {
            grants.Add("RawDatabase.Manage");
        }
        var result = await ContentAgeOptionEndpoints.ListAuthorized(
            new DefaultHttpContext(), context, Authorization([..grants]), deleted);
        Assert.Equal(status, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode ?? StatusCodes.Status200OK);
    }
}
