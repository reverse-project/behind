using Behind.Common;
using Behind.Common.Database;
using Behind.Common.Database.Raw;
using Behind.Common.Database.Modules;
using Behind.Common.Modules;
using Behind.Common.WebApi;
using Behind.Contact.Database;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Domain;
using Behind.Core.WebApi.EmailSender;
using Behind.Contact.Database.Commands;
using Behind.Contact.Database.Postgres;
using Behind.Contact.Database.Sqlite;
using Behind.Contact.Domain;
using Behind.Contact.WebApi;
using Behind.Contact.WebApi.Endpoints;
using Behind.Web.Database.Sqlite;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace Behind.Tests.Contact;

public sealed class ContactBackendTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ModuleCatalog _catalog = ModuleCatalog.Create([ContactModule.Descriptor, Behind.Quotes.WebApi.QuotesModule.Descriptor, Behind.Sport.WebApi.SportModule.Descriptor, Behind.Forum.WebApi.ForumModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);
    private readonly IModuleUsers _users = Substitute.For<IModuleUsers>();

    public ContactBackendTests()
    {
        _connection.Open();
        using var context = Context();
        context.Database.EnsureCreated();
    }

    private WebContextSqlite Context() => new(new DbContextOptionsBuilder<WebContextSqlite>().UseSqlite(_connection).AddInterceptors(new AuditSaveChangesInterceptor(() => new FixedAuditActorProvider(AuditActor.Anonymous), TimeProvider.System)).Options, _catalog);
    private static SaveContactForm Form() => new(null, "Help", "Ask us", "admin@example.org", "choices", ["Question"], [new("account", "Account", "text", true, 50)]);
    private static SubmitContactRequest Submission() => new("<Visitor>", "person@example.org", "Question", "<script>alert(1)</script>", new() { ["account"] = "<account>" });
    private static ContactForm Draft(SaveContactForm x) => new(x.Name, x.Description, x.Recipient ?? "", x.SubjectMode, ContactValidation.Json(x.Subjects), ContactValidation.Json(x.Fields));

    [Fact]
    public void FlattenedModels_AreCachedSeparatelyForInstalledModules()
    {
        var withoutContact = ModuleCatalog.Create(
            _catalog.Modules.Where(module => module.Key != "contact").ToArray()).GetOrElse(ModuleCatalog.Empty);
        var options = new DbContextOptionsBuilder<WebContextSqlite>().UseSqlite(_connection).Options;
        using var absent = new WebContextSqlite(options, withoutContact);
        using var installed = new WebContextSqlite(options, _catalog);

        Assert.Null(absent.Model.FindEntityType(typeof(ContactForm)));
        Assert.Null(absent.Model.FindEntityType(typeof(ContactRequest)));
        Assert.NotNull(installed.Model.FindEntityType(typeof(ContactForm)));
        Assert.NotNull(installed.Model.FindEntityType(typeof(ContactRequest)));
        Assert.NotSame(absent.Model, installed.Model);
    }

    [Fact]
    public async Task FormUpdates_RejectStaleVersionsAndPropagateStorageErrors()
    {
        await using var context = Context();
        var commands = new ContactFormCommands(context);
        var created = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(Draft(Form())));
        var updated = Assert.IsType<VersionedSaveResult.Saved>(await commands.UpdateExpected(
            created.Id.Key, created.Id.Version, Draft(Form() with { Name = "Updated" }), CancellationToken.None));

        Assert.IsType<VersionedSaveResult.VersionConflict>(await commands.UpdateExpected(
            created.Id.Key, created.Id.Version, Draft(Form()), CancellationToken.None));

        await context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER ContactStorageFailure BEFORE INSERT ON ContactForms BEGIN SELECT RAISE(ABORT, 'storage unavailable'); END;");

        await Assert.ThrowsAsync<DbUpdateException>(() => commands.UpdateExpected(
            updated.Id.Key, updated.Id.Version, Draft(Form()), CancellationToken.None));
    }

    [Fact]
    public void Descriptor_RegistersAdministrationAndWidget()
    {
        Assert.True(ModuleCatalog.Create([ContactModule.Descriptor]).IsValid);
        Assert.Equal(ModuleAudience.Administration, Assert.Single(ContactModule.Descriptor.GrantAreas).Audience);
        Assert.Equal("contact.form", Assert.Single(ContactModule.Descriptor.Widgets).Key);
    }

    [Fact]
    public void Routes_ProtectManagementAndRateLimitPublicSubmission()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddContactDatabase();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(Substitute.For<IModuleEmailSender>());
        builder.Services.AddSingleton(Substitute.For<IModuleIdentity>());
        builder.Services.AddSingleton(Substitute.For<IModuleCreationOrder>());
        builder.Services.AddSingleton(_users);
        var app = builder.Build();
        app.MapContactEndpoints();
        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>().ToList();
        Assert.All(routes.Where(route => route.RoutePattern.RawText!.StartsWith("/contact/manage/", StringComparison.Ordinal)),
            route => Assert.NotEmpty(route.Metadata.GetOrderedMetadata<IAuthorizeData>()));
        var submission = Assert.Single(routes, route => route.RoutePattern.RawText == "/contact/forms/{id:guid}/requests");
        Assert.Equal(ContactWebApiExtensions.SubmissionPolicy, submission.Metadata.GetMetadata<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    public async Task PublicForm_HidesRecipientAndHonorsLocaleFilter()
    {
        await using var context = Context();
        var form = Assert.IsType<VersionedSaveResult.Saved>(await new ContactFormCommands(context).Create(Draft(Form())));
        var response = await ContactEndpoints.Form(form.Id.Key, new DefaultHttpContext(), context);
        var visible = Assert.IsType<ContactFormResponse>(((IValueHttpResult)response).Value);
        Assert.Equal("Help", visible.Name);
        Assert.DoesNotContain("recipient", System.Text.Json.JsonSerializer.Serialize(visible), StringComparison.OrdinalIgnoreCase);
        context.IncludeAllContentLocales = false;
        context.HiddenLocaleContentKeys = [$"ContactForm:{form.Id.Key.ToString().ToLowerInvariant()}"];
        var hidden = await ContactEndpoints.Form(form.Id.Key, new DefaultHttpContext(), context);
        Assert.Equal(404, ((IStatusCodeHttpResult)hidden).StatusCode);
    }

    [Fact]
    public void Validation_EnforcesFormConstraintsAndSubmittedFields()
    {
        var form = Form();
        Assert.True(ContactValidation.Valid(form));
        Assert.True(ContactValidation.Valid(form with { Recipient = "" }));
        Assert.True(ContactValidation.Valid(form with { Recipient = null }));
        Assert.False(ContactValidation.Valid(form with { Recipient = " " }));
        Assert.False(ContactValidation.Valid(form with { Recipient = "not-email" }));
        Assert.False(ContactValidation.Valid(form with { Fields = [new("x", "X", "unknown", false, 1)] }));
        Assert.False(ContactValidation.Valid(form with { Fields = [new("x", "X", "text", false, 1), new("x", "Again", "text", false, 1)] }));
        Assert.True(ContactValidation.Valid(Submission(), Draft(form), out var values));
        Assert.Equal("Account", Assert.Single(values).Label);
        Assert.False(ContactValidation.Valid(Submission() with { Subject = "Other" }, Draft(form), out _));
        Assert.False(ContactValidation.Valid(Submission() with { Fields = new() { ["other"] = "x" } }, Draft(form), out _));
        Assert.False(ContactValidation.Valid(Submission() with { Fields = [] }, Draft(form), out _));
    }

    [Fact]
    public async Task InboxOnlySubmission_IsSavedWithoutSendingAndCannotBeRetried()
    {
        await using var context = Context();
        var forms = new ContactFormCommands(context);
        var created = await ContactEndpoints.CreateForm(Form() with { Recipient = null }, new DefaultHttpContext(), forms);
        Assert.IsType<VersionedSavedResponse>(((IValueHttpResult)created).Value);
        var form = Assert.Single(await context.ContactForms.LatestOf(context.ContactForms).ToKeyedAsync());
        Assert.Equal("", form.Entity.Recipient);
        var commands = new ContactRequestCommands(context);
        var sender = new RecordingSender(() => throw new InvalidOperationException("Email must not be sent"));

        var receipt = await ContactEndpoints.Submit(form.Key, Submission(), new DefaultHttpContext(), context, commands, sender, NullLoggerFactory.Instance);

        Assert.Equal(201, ((IStatusCodeHttpResult)receipt).StatusCode);
        Assert.Equal("notRequested", Assert.IsType<ContactReceipt>(((IValueHttpResult)receipt).Value).DeliveryStatus);
        var saved = Assert.Single(await context.ContactRequests.LatestOf(context.ContactRequests).ToKeyedAsync());
        Assert.Equal("notRequested", saved.Entity.DeliveryStatus);
        Assert.Equal("", saved.Entity.Recipient);
        Assert.Equal(Submission().Message, saved.Entity.Message);
        Assert.Equal("", sender.Recipient);
        Assert.IsType<VersionedSaveResult.Saved>(await forms.UpdateExpected(form.Key, form.Version, Draft(Form()), CancellationToken.None));
        Assert.Equal(409, ((IStatusCodeHttpResult)await ContactEndpoints.Retry(saved.Key, context, commands, sender, NullLoggerFactory.Instance)).StatusCode);
        Assert.Equal("notRequested", Assert.Single(await context.ContactRequests.LatestOf(context.ContactRequests).ToListAsync()).DeliveryStatus);
    }

    [Fact]
    public async Task Submission_PersistsBeforeDeliverySnapshotsAndEncodes()
    {
        await using var context = Context();
        var form = Assert.IsType<VersionedSaveResult.Saved>(await new ContactFormCommands(context).Create(Draft(Form())));
        var sender = new RecordingSender(() => Assert.True(context.ContactRequests.Any()));
        var result = await ContactEndpoints.Submit(form.Id.Key, Submission(), new DefaultHttpContext(), context, new ContactRequestCommands(context), sender, NullLoggerFactory.Instance);
        Assert.Equal(201, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal("sent", Assert.IsType<ContactReceipt>(((IValueHttpResult)result).Value).DeliveryStatus);
        var row = Assert.Single(await context.ContactRequests.LatestOf(context.ContactRequests).ToListAsync());
        Assert.Equal("sent", row.DeliveryStatus);
        Assert.Equal("admin@example.org", sender.Recipient);
        Assert.Contains("&lt;script&gt;", sender.Body);
        Assert.Contains("&lt;account&gt;", sender.Body);
        Assert.DoesNotContain("<script>", sender.Body);
        Assert.Contains("Account", row.FieldsJson);
    }

    [Fact]
    public async Task LoggerDelivery_RecordsSubmissionAsSent()
    {
        await using var context = Context();
        var form = Assert.IsType<VersionedSaveResult.Saved>(await new ContactFormCommands(context).Create(Draft(Form())));
        var commands = new ContactRequestCommands(context);
        var sender = new ModuleEmailSender(new LoggerEmailSender(NullLogger<LoggerEmailSender>.Instance, FakeHostEnvironment.Development));

        var receipt = await ContactEndpoints.Submit(form.Id.Key, Submission(), new DefaultHttpContext(), context, commands, sender, NullLoggerFactory.Instance);

        Assert.Equal("sent", Assert.IsType<ContactReceipt>(((IValueHttpResult)receipt).Value).DeliveryStatus);
        var saved = Assert.Single(await context.ContactRequests.LatestOf(context.ContactRequests).ToKeyedAsync());
        Assert.Equal("sent", saved.Entity.DeliveryStatus);
        Assert.Equal(409, ((IStatusCodeHttpResult)await ContactEndpoints.Retry(saved.Key, context, commands, sender, NullLoggerFactory.Instance)).StatusCode);
    }

    [Fact]
    public async Task FailureIsRetainedAndRetryUsesSnapshot()
    {
        await using var context = Context();
        var forms = new ContactFormCommands(context);
        var form = Assert.IsType<VersionedSaveResult.Saved>(await forms.Create(Draft(Form())));
        var commands = new ContactRequestCommands(context);
        var sender = new RecordingSender(() => throw new InvalidOperationException("smtp unavailable"));
        var receipt = await ContactEndpoints.Submit(form.Id.Key, Submission(), new DefaultHttpContext(), context, commands, sender, NullLoggerFactory.Instance);
        Assert.Equal(201, ((IStatusCodeHttpResult)receipt).StatusCode);
        Assert.Equal("failed", Assert.IsType<ContactReceipt>(((IValueHttpResult)receipt).Value).DeliveryStatus);
        Assert.StartsWith("/contact/manage/requests/", ((Microsoft.AspNetCore.Http.HttpResults.Created<ContactReceipt>)receipt).Location);
        var first = Assert.Single(await context.ContactRequests.LatestOf(context.ContactRequests).ToKeyedAsync());
        Assert.Equal("failed", first.Entity.DeliveryStatus);
        Assert.IsType<VersionedSaveResult.Saved>(await forms.UpdateExpected(form.Id.Key, form.Id.Version, Draft(Form() with { Recipient = "new@example.org" }), CancellationToken.None));
        sender.Action = () => { };
        var result = await ContactEndpoints.Retry(first.Key, context, commands, sender, NullLoggerFactory.Instance);
        Assert.Equal(204, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal("admin@example.org", sender.Recipient);
        Assert.Equal("sent", Assert.Single(await context.ContactRequests.LatestOf(context.ContactRequests).ToListAsync()).DeliveryStatus);
    }

    [Fact]
    public async Task ProviderHttpFailure_IsRetainedForRetry()
    {
        await using var context = Context();
        var form = Assert.IsType<VersionedSaveResult.Saved>(await new ContactFormCommands(context).Create(Draft(Form())));
        var commands = new ContactRequestCommands(context);
        var sender = new RecordingSender(() => throw new HttpRequestException("Provider unavailable"));

        var receipt = await ContactEndpoints.Submit(form.Id.Key, Submission(), new DefaultHttpContext(), context, commands, sender, NullLoggerFactory.Instance);

        Assert.Equal("failed", Assert.IsType<ContactReceipt>(((IValueHttpResult)receipt).Value).DeliveryStatus);
        Assert.Equal("failed", Assert.Single(await context.ContactRequests.LatestOf(context.ContactRequests).ToListAsync()).DeliveryStatus);
    }

    [Fact]
    public async Task SubmissionTimeout_ReturnsUncertainReceiptAndDoesNotRetry()
    {
        await using var context = Context();
        var form = Assert.IsType<VersionedSaveResult.Saved>(await new ContactFormCommands(context).Create(Draft(Form())));
        var commands = new ContactRequestCommands(context);
        var sender = new RecordingSender(() => throw new TimeoutException());
        var receipt = await ContactEndpoints.Submit(form.Id.Key, Submission(), new DefaultHttpContext(), context, commands, sender, NullLoggerFactory.Instance);
        Assert.Equal("sending", Assert.IsType<ContactReceipt>(((IValueHttpResult)receipt).Value).DeliveryStatus);
        Assert.True(sender.CancellationToken.CanBeCanceled);
        var current = Assert.Single(await context.ContactRequests.LatestOf(context.ContactRequests).ToKeyedAsync());
        Assert.Equal("sending", current.Entity.DeliveryStatus);
        Assert.Equal(409, ((IStatusCodeHttpResult)await ContactEndpoints.Retry(current.Key, context, commands, sender, NullLoggerFactory.Instance)).StatusCode);
    }

    [Fact]
    public async Task ReadDeletionAndAuditLabelsUseVersionedRecords()
    {
        await using var context = Context();
        var form = Assert.IsType<VersionedSaveResult.Saved>(await new ContactFormCommands(context).Create(Draft(Form())));
        var commands = new ContactRequestCommands(context);
        var saved = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(new ContactRequest(form.Id.Key, "Help", "admin@example.org", "Visitor", "v@example.org", "Question", "Hi", "[]", "failed", null)));
        var admin = Guid.NewGuid();
        var result = await ContactEndpoints.Read(saved.Id.Key, new(true), new DefaultHttpContext(), context, commands, new Identity(admin));
        Assert.Equal(204, ((IStatusCodeHttpResult)result).StatusCode);
        Assert.Equal(admin, Assert.Single(await context.ContactRequests.LatestOf(context.ContactRequests).ToListAsync()).ReadBy);
        var labels = await new ContactAuditLabelSource(context).LabelsAsync([form.Id.Key, saved.Id.Key]);
        Assert.Equal(2, labels.Count);
        Assert.All(labels, label => Assert.Equal("Help", label.Label));
        Assert.True(await commands.Delete(saved.Id.Key));
        Assert.True(await commands.Restore(saved.Id.Key));
        Assert.True(await commands.Delete(saved.Id.Key));
        Assert.Equal(HardDeleteResult.Deleted, await commands.HardDelete(saved.Id.Key));
    }

    [Fact]
    public async Task InboxSearchPaginationAndUnreadFilter_UseLatestVersionsAndDeletionState()
    {
        await using var context = Context();
        var commands = new ContactRequestCommands(context);
        var form = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var first = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
            new ContactRequest(form, "Billing", "", "Alice", "alice@example.org", "Invoice", "payment details", "[]", "notRequested", null)));
        var second = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
            new ContactRequest(form, "Support", "", "Bob", "bob@example.org", "Help", "Need advice", "[]", "notRequested", null)));
        var third = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
            new ContactRequest(form, "Support", "", "Carol", "carol@example.org", "Help", "Other message", "[]", "notRequested", null)));
        await ContactEndpoints.Read(second.Id.Key, new(true), new DefaultHttpContext(), context, commands, new Identity(admin));
        Assert.True(await commands.Delete(third.Id.Key));
        var http = new DefaultHttpContext();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<object?>(), ManagedListExtensions.DeletedRecordsGrant).Returns(AuthorizationResult.Success());
        var order = new ModuleCreationOrder(context);

        async Task<PageResponse<ContactRequestResponse>> List(int? page = null, int? pageSize = null, string? search = null, bool unreadOnly = false, bool deleted = false)
        {
            var result = await ContactEndpoints.ManageRequests(http, context, auth, order, _users, deleted, page, pageSize, search, unreadOnly);
            return Assert.IsType<PageResponse<ContactRequestResponse>>(((IValueHttpResult)result).Value);
        }

        var expected = await order.NewestFirst(((IContactContext)context).InDeletionState(context.ContactRequests, DeletionState.Active).LatestOf(context.ContactRequests)).ToKeyedAsync();
        var page1 = await List(page: 1, pageSize: 1);
        var page2 = await List(page: 2, pageSize: 1);
        Assert.Equal(2, page1.Total);
        Assert.Equal(2, page1.PageCount);
        Assert.Equal(expected.Select(x => x.Key), page1.Items.Concat(page2.Items).Select(x => x.Id));
        Assert.Equal("no-store", http.Response.Headers.CacheControl.ToString());
        Assert.Equal(first.Id.Key, Assert.Single((await List(unreadOnly: true)).Items).Id);
        Assert.Empty((await List(search: "BOB", unreadOnly: true)).Items);
        Assert.Equal(second.Id.Key, Assert.Single((await List(search: "  BOB@EXAMPLE.ORG  ")).Items).Id);
        Assert.Equal(first.Id.Key, Assert.Single((await List(search: "PAYMENT")).Items).Id);
        Assert.Equal(first.Id.Key, Assert.Single((await List(search: "billing")).Items).Id);
        Assert.Equal(first.Id.Key, Assert.Single((await List(search: "invoice")).Items).Id);
        Assert.Equal(first.Id.Key, Assert.Single((await List(search: "alice")).Items).Id);
        Assert.Equal(2, (await List(search: " ")).Total);
        Assert.Equal(third.Id.Key, Assert.Single((await List(search: "CAROL", unreadOnly: true, deleted: true)).Items).Id);
        Assert.Empty((await List(search: "CAROL")).Items);
        Assert.Equal(100, (await List(pageSize: 1000)).PageSize);
        Assert.Equal(1, (await List(page: -1, pageSize: 0)).Page);
        Assert.Empty((await List(page: 5, pageSize: 1)).Items);

        var denied = Substitute.For<IAuthorizationService>();
        denied.AuthorizeAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<object?>(), ManagedListExtensions.DeletedRecordsGrant).Returns(AuthorizationResult.Failed());
        Assert.Equal(403, ((IStatusCodeHttpResult)await ContactEndpoints.ManageRequests(http, context, denied, order, _users, deleted: true)).StatusCode);
        Assert.Equal(400, ((IStatusCodeHttpResult)await ContactEndpoints.ManageRequests(http, context, auth, order, _users, page: int.MaxValue, pageSize: 100)).StatusCode);
    }

    [Fact]
    public async Task InboxReaderLabels_ResolveDistinctProfilesAndPreserveReadState()
    {
        await using var context = Context();
        var commands = new ContactRequestCommands(context);
        var known = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var form = Guid.NewGuid();
        async Task<Guid> Create(string name, Guid? reader)
        {
            var saved = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
                new ContactRequest(form, "Help", "", name, "user@example.org", "Question", "Message", "[]", "notRequested", reader)));
            return saved.Id.Key;
        }

        var first = await Create("First", known);
        await Create("Second", known);
        var deletedId = await Create("Deleted", missing);
        await Create("Unread", null);
        var missingId = await Create("Missing", missing);
        Assert.True(await commands.Delete(deletedId));
        var label = known.ToUserLabel("Administrator");
        _users.Find(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IModuleUsers.Profile>>([new(known, label, null)]));
        using var cancellation = new CancellationTokenSource();
        var http = new DefaultHttpContext { RequestAborted = cancellation.Token };
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<object?>(), ManagedListExtensions.DeletedRecordsGrant).Returns(AuthorizationResult.Success());
        var order = new ModuleCreationOrder(context);

        var response = await ContactEndpoints.ManageRequests(http, context, auth, order, _users);
        var page = Assert.IsType<PageResponse<ContactRequestResponse>>(((IValueHttpResult)response).Value);
        Assert.Equal(label, Assert.Single(page.Items, item => item.Id == first).ReadByName);
        Assert.Equal(missing.ToUserLabel(null), Assert.Single(page.Items, item => item.Id == missingId).ReadByName);
        Assert.Null(Assert.Single(page.Items, item => item.Name == "Unread").ReadByName);
        await _users.Received(1).Find(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(known) && ids.Contains(missing)), cancellation.Token);

        var detail = await ContactEndpoints.ManageRequest(first, http, context, auth, order, _users);
        var item = Assert.IsType<ContactRequestResponse>(((IValueHttpResult)detail).Value);
        Assert.Equal(known, item.ReadBy);
        Assert.Equal(label, item.ReadByName);
        var deleted = await ContactEndpoints.ManageRequest(deletedId, http, context, auth, order, _users, true);
        Assert.Equal(missing.ToUserLabel(null), Assert.IsType<ContactRequestResponse>(((IValueHttpResult)deleted).Value).ReadByName);

        _users.ClearReceivedCalls();
        await ContactEndpoints.ManageRequests(http, context, auth, order, _users, unreadOnly: true);
        await _users.DidNotReceive().Find(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        var denied = Substitute.For<IAuthorizationService>();
        denied.AuthorizeAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<object?>(), ManagedListExtensions.DeletedRecordsGrant).Returns(AuthorizationResult.Failed());
        Assert.Equal(403, ((IStatusCodeHttpResult)await ContactEndpoints.ManageRequest(deletedId, http, context, denied, order, _users, true)).StatusCode);
        await _users.DidNotReceive().Find(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InboxEndpoint_BindsSearchAndPagingFromHttpQuery()
    {
        await using var context = Context();
        var commands = new ContactRequestCommands(context);
        var form = Guid.NewGuid();
        var reader = Guid.NewGuid();
        var readerLabel = reader.ToUserLabel("Administrator");
        _users.Find(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IModuleUsers.Profile>>([new(reader, readerLabel, null)]));
        await commands.Create(new ContactRequest(form, "Help", "", "Ada", "ada@example.org", "Question", "Test message", "[]", "notRequested", null));
        await commands.Create(new ContactRequest(form, "Help", "", "Bob", "bob@example.org", "Question", "Test message", "[]", "notRequested", reader));
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IContactContext>(context);
        builder.Services.AddSingleton<IAuthorizationService>(Substitute.For<IAuthorizationService>());
        builder.Services.AddSingleton<IModuleCreationOrder>(new ModuleCreationOrder(context));
        builder.Services.AddSingleton(_users);
        await using var app = builder.Build();
        app.MapGet("/contact/manage/requests", ContactEndpoints.ManageRequests);
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };

        var result = await client.GetFromJsonAsync<PageResponse<ContactRequestResponse>>(
            "/contact/manage/requests?page=1&pageSize=1&search=question&unreadOnly=true");

        Assert.NotNull(result);
        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.PageSize);
        var item = Assert.Single(result.Items);
        Assert.Equal("Ada", item.Name);
        Assert.NotNull(item.CreatedAt);
        Assert.Null(item.ReadByName);
        var readPage = await client.GetFromJsonAsync<PageResponse<ContactRequestResponse>>("/contact/manage/requests?search=Bob");
        Assert.NotNull(readPage);
        var readItem = Assert.Single(readPage.Items);
        Assert.Equal(reader, readItem.ReadBy);
        Assert.Equal(readerLabel, readItem.ReadByName);
        await app.StopAsync();
    }

    [Fact]
    public async Task InboxCreationTime_ComesFromFirstVersionAuditForListAndDetails()
    {
        await using var context = Context();
        var commands = new ContactRequestCommands(context);
        var saved = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(
            new ContactRequest(Guid.NewGuid(), "Help", "", "Ada", "ada@example.org", "Question", "Message", "[]", "notRequested", null)));
        var audit = await context.AuditLogEntries.AsNoTracking().Where(entry =>
            entry.EntityName == nameof(ContactRequest) && entry.EntityKey == $"{saved.Id.Key}:1"
            && entry.Action == new AuditLogEntry.Created()).SingleAsync();
        var order = new ModuleCreationOrder(context);
        var missing = Guid.NewGuid();
        var times = await order.CreatedAtAsync<ContactRequest>([saved.Id.Key, saved.Id.Key, missing]);
        Assert.Equal(audit.Timestamp, times[saved.Id.Key]);
        Assert.Null(times[missing]);
        Assert.Empty(await order.CreatedAtAsync<ContactRequest>([]));
        var http = new DefaultHttpContext();
        var auth = Substitute.For<IAuthorizationService>();
        auth.AuthorizeAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<object?>(), ManagedListExtensions.DeletedRecordsGrant).Returns(AuthorizationResult.Success());
        await ContactEndpoints.Read(saved.Id.Key, new(true), http, context, commands, new Identity(Guid.NewGuid()));

        var listResult = await ContactEndpoints.ManageRequests(http, context, auth, order, _users);
        var list = Assert.IsType<PageResponse<ContactRequestResponse>>(((IValueHttpResult)listResult).Value);
        Assert.Equal(audit.Timestamp, Assert.Single(list.Items).CreatedAt);
        var detail = await ContactEndpoints.ManageRequest(saved.Id.Key, http, context, auth, order, _users);
        Assert.Equal(audit.Timestamp, Assert.IsType<ContactRequestResponse>(((IValueHttpResult)detail).Value).CreatedAt);
        Assert.True(await commands.Delete(saved.Id.Key));
        var deleted = await ContactEndpoints.ManageRequest(saved.Id.Key, http, context, auth, order, _users, true);
        Assert.Equal(audit.Timestamp, Assert.IsType<ContactRequestResponse>(((IValueHttpResult)deleted).Value).CreatedAt);
        var deletedList = await ContactEndpoints.ManageRequests(http, context, auth, order, _users, deleted: true);
        Assert.Equal(audit.Timestamp, Assert.Single(Assert.IsType<PageResponse<ContactRequestResponse>>(((IValueHttpResult)deletedList).Value).Items).CreatedAt);
        var auditKey = $"{saved.Id.Key}:1";
        await context.Database.ExecuteSqlAsync($"DELETE FROM \"AuditLogEntries\" WHERE \"EntityName\" = {nameof(ContactRequest)} AND \"EntityKey\" = {auditKey}");
        deleted = await ContactEndpoints.ManageRequest(saved.Id.Key, http, context, auth, order, _users, true);
        Assert.Null(Assert.IsType<ContactRequestResponse>(((IValueHttpResult)deleted).Value).CreatedAt);
        deletedList = await ContactEndpoints.ManageRequests(http, context, auth, order, _users, deleted: true);
        Assert.Null(Assert.Single(Assert.IsType<PageResponse<ContactRequestResponse>>(((IValueHttpResult)deletedList).Value).Items).CreatedAt);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => order.CreatedAtAsync<ContactRequest>([saved.Id.Key], cancellation.Token));
    }

    [Fact]
    public async Task DeletedDetails_RequireDeveloperPermissionAndCanBeRestored()
    {
        await using var context = Context();
        var forms = new ContactFormCommands(context);
        var form = Assert.IsType<VersionedSaveResult.Saved>(await forms.Create(Draft(Form())));
        var commands = new ContactRequestCommands(context);
        var saved = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(new ContactRequest(form.Id.Key, "Help", "admin@example.org", "Visitor", "v@example.org", "Question", "Hi", "[]", "sending", null)));
        Assert.Equal(204, ((IStatusCodeHttpResult)await ContactEndpoints.DeleteRequest(saved.Id.Key, context, commands, 0)).StatusCode);
        Assert.True(await forms.Delete(form.Id.Key));
        var http = new DefaultHttpContext();
        var authorized = Substitute.For<IAuthorizationService>();
        authorized.AuthorizeAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<object?>(), ManagedListExtensions.DeletedRecordsGrant).Returns(AuthorizationResult.Success());
        var denied = Substitute.For<IAuthorizationService>();
        denied.AuthorizeAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), Arg.Any<object?>(), ManagedListExtensions.DeletedRecordsGrant).Returns(AuthorizationResult.Failed());
        Assert.Equal(404, ((IStatusCodeHttpResult)await ContactEndpoints.Form(form.Id.Key, http, context)).StatusCode);
        var order = new ModuleCreationOrder(context);
        Assert.Equal(403, ((IStatusCodeHttpResult)await ContactEndpoints.ManageRequest(saved.Id.Key, http, context, denied, order, _users, true)).StatusCode);
        var request = await ContactEndpoints.ManageRequest(saved.Id.Key, http, context, authorized, order, _users, true);
        Assert.True(Assert.IsType<ContactRequestResponse>(((IValueHttpResult)request).Value).IsDeleted);
        var formResult = await ContactEndpoints.ManageForm(form.Id.Key, http, context, authorized, true);
        Assert.True(Assert.IsType<ManagedContactForm>(((IValueHttpResult)formResult).Value).IsDeleted);
        Assert.Equal(204, ((IStatusCodeHttpResult)await ContactEndpoints.DeleteRequest(saved.Id.Key, context, commands, 1)).StatusCode);
        Assert.Equal(204, ((IStatusCodeHttpResult)await ContactEndpoints.DeleteRequest(saved.Id.Key, context, commands, 0)).StatusCode);
        Assert.Equal(204, ((IStatusCodeHttpResult)await ContactEndpoints.DeleteRequest(saved.Id.Key, context, commands, 2)).StatusCode);
        Assert.True(await forms.Restore(form.Id.Key));
    }

    [Fact]
    public async Task Retry_RejectsInProgressAndSentWithoutSendingAgain()
    {
        await using var context = Context();
        var commands = new ContactRequestCommands(context);
        var saved = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(new ContactRequest(Guid.NewGuid(), "Help", "admin@example.org", "Visitor", "v@example.org", "Question", "Hi", "[]", "sending", null)));
        var sender = new RecordingSender(() => throw new Exception("must not send"));
        Assert.Equal(409, ((IStatusCodeHttpResult)await ContactEndpoints.Retry(saved.Id.Key, context, commands, sender, NullLoggerFactory.Instance)).StatusCode);
        Assert.Equal("", sender.Recipient);
    }

    [Fact]
    public async Task ConcurrentRetryAndRead_KeepSingleDeliveryAndReadState()
    {
        await using var first = Context();
        await using var second = Context();
        var commands = new ContactRequestCommands(first);
        var saved = Assert.IsType<VersionedSaveResult.Saved>(await commands.Create(new ContactRequest(Guid.NewGuid(), "Help", "admin@example.org", "Visitor", "v@example.org", "Question", "Hi", "[]", "failed", null)));
        var sender = new BlockingSender();
        var delivery = ContactEndpoints.Retry(saved.Id.Key, first, commands, sender, NullLoggerFactory.Instance);
        await sender.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var otherCommands = new ContactRequestCommands(second);
        var duplicate = await ContactEndpoints.Retry(saved.Id.Key, second, otherCommands, sender, NullLoggerFactory.Instance);
        Assert.Equal(409, ((IStatusCodeHttpResult)duplicate).StatusCode);
        var admin = Guid.NewGuid();
        var read = await ContactEndpoints.Read(saved.Id.Key, new(true), new DefaultHttpContext(), second, otherCommands, new Identity(admin));
        Assert.Equal(204, ((IStatusCodeHttpResult)read).StatusCode);
        sender.Release.SetResult();
        Assert.Equal(204, ((IStatusCodeHttpResult)await delivery).StatusCode);
        var current = Assert.Single(await second.ContactRequests.LatestOf(second.ContactRequests).ToKeyedAsync());
        Assert.Equal(admin, current.Entity.ReadBy);
        Assert.Equal("sent", current.Entity.DeliveryStatus);
        Assert.Equal(1, sender.Count);
    }

    [Fact]
    public async Task DeletingDuringDelivery_DoesNotRecreateRequest()
    {
        await using var first = Context();
        await using var second = Context();
        var form = Assert.IsType<VersionedSaveResult.Saved>(await new ContactFormCommands(first).Create(Draft(Form())));
        var sender = new BlockingSender();
        var submission = ContactEndpoints.Submit(form.Id.Key, Submission(), new DefaultHttpContext(), first, new ContactRequestCommands(first), sender, NullLoggerFactory.Instance);
        await sender.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var current = Assert.Single(await second.ContactRequests.LatestOf(second.ContactRequests).ToKeyedAsync());
        Assert.Equal(204, ((IStatusCodeHttpResult)await ContactEndpoints.DeleteRequest(current.Key, second, new ContactRequestCommands(second), 0)).StatusCode);
        sender.Release.SetResult();
        var receipt = await submission;
        Assert.Equal("sending", Assert.IsType<ContactReceipt>(((IValueHttpResult)receipt).Value).DeliveryStatus);
        Assert.Empty(await ((IContactContext)second).InDeletionState(second.ContactRequests, DeletionState.Active).ToListAsync());
    }

    [Fact]
    public async Task DatabaseErrorsAreNotReportedAsMissingOrConflict()
    {
        await using var context = Context();
        var commands = new ContactRequestCommands(context);
        await _connection.CloseAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => ContactEndpoints.Read(Guid.NewGuid(), new(true), new DefaultHttpContext(), context, commands, new Identity(Guid.NewGuid())));
    }

    [Fact]
    public void CorruptSchemasAreRejectedAndHeadersValidated()
    {
        Assert.Throws<InvalidOperationException>(() => ContactValidation.Schema(Draft(Form()) with { FieldsJson = "null" }));
        Assert.Throws<InvalidOperationException>(() => ContactValidation.Schema(Draft(Form()) with { SubjectMode = "unknown" }));
        Assert.Throws<System.Text.Json.JsonException>(() => ContactValidation.Schema(Draft(Form()) with { SubjectsJson = "{" }));
        Assert.False(ContactValidation.Valid(Form() with { Subjects = ["Bad\r\nBcc: evil"] }));
        Assert.False(ContactValidation.Valid(Form() with { Recipient = "Name <a@example.org>" }));
        Assert.False(ContactValidation.Valid(Submission() with { Name = "bad\rname" }, Draft(Form()), out _));
    }

    [Fact]
    public void RawEditor_MasksSensitiveColumns()
    {
        using var context = Context();
        var table = Assert.Single(context.RawTables, x => x.Name == "ContactRequests");
        Assert.All(new[] { "Name", "Email", "Subject", "Message", "Recipient", "FieldsJson" }, name => Assert.True(Assert.Single(table.Columns, column => column.Name == name).IsSensitive));
    }

    [Fact]
    public async Task Migrations_ApplySqliteAndGeneratePostgresScript()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var sqlite = new ContactContextSqlite(new DbContextOptionsBuilder<ContactContextSqlite>().UseSqlite(connection).Options);
        await sqlite.Database.MigrateAsync();
        Assert.Empty(await sqlite.Database.GetPendingMigrationsAsync());
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('ContactForms', 'ContactRequests')";
        Assert.Equal(2L, (long)(await command.ExecuteScalarAsync())!);
        await using var postgres = new ContactContextPostgresFactory().CreateDbContext([]);
        var script = postgres.GetService<IMigrator>().GenerateScript();
        Assert.Contains("ContactForms", script);
        Assert.Contains("ContactRequests", script);
    }

    public void Dispose() => _connection.Dispose();

    private sealed class RecordingSender(Action action) : IModuleEmailSender
    {
        public Action Action { get; set; } = action;
        public string Recipient { get; private set; } = "";
        public string Body { get; private set; } = "";
        public CancellationToken CancellationToken { get; private set; }
        public Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken)
        {
            Recipient = email;
            Body = htmlMessage;
            CancellationToken = cancellationToken;
            Action();
            return Task.CompletedTask;
        }
    }

    private sealed class Identity(Guid id) : IModuleIdentity
    {
        public Task<Guid?> UserId(HttpContext httpContext) => Task.FromResult<Guid?>(id);
    }

    private sealed class BlockingSender : IModuleEmailSender
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Count { get; private set; }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken)
        {
            Count++;
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }
}
