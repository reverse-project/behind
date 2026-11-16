using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using System.Security.Claims;
using Behind.Common;
using Behind.Common.Database.Modules;
using Behind.Common.WebApi;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Domain;
using Behind.Core.WebApi;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class PrivateMessagingHttpTests : IAsyncLifetime
{
    private readonly TestDbContextFactory _factory = new();
    private WebApplication _application = null!;
    private HttpClient _client = null!;
    private Guid _sender;
    private Guid _recipient;
    private Guid _outsider;

    private sealed class HeaderIdentity : IModuleIdentity
    {
        public Task<Guid?> UserId(HttpContext context) =>
            Task.FromResult<Guid?>(Guid.TryParse(context.Request.Headers["Test-User"], out var id) ? id : null);
    }

    private sealed class HeaderAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(Guid.TryParse(Request.Headers["Test-User"], out var id)
                ? AuthenticateResult.Success(new AuthenticationTicket(
                    new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], Scheme.Name)), Scheme.Name))
                : AuthenticateResult.NoResult());
    }

    public async Task InitializeAsync()
    {
        await using (var context = _factory.CreateContext())
        {
            _sender = await DatabaseSeedHelper.SeedHumanUserAsync(context, "sender@example.test");
            _recipient = await DatabaseSeedHelper.SeedHumanUserAsync(context, "recipient@example.test");
            _outsider = await DatabaseSeedHelper.SeedHumanUserAsync(context, "outsider@example.test");
        }

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var accessor = new HttpContextAccessor();
        builder.Services.AddSingleton<IHttpContextAccessor>(accessor);
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>("Test", _ => { });
        builder.Services.AddAuthorization(options =>
        {
            foreach (var grant in new[]
                     {
                         "PrivateMessage.Read", "PrivateMessage.Write", "PrivateMessage.Delete",
                         "PrivateMessageModeration.Manage", "Report.Write", "Report.Manage", "RawDatabase.Manage"
                     })
            {
                options.AddPolicy(grant, policy => policy.RequireAssertion(context =>
                    (context.Resource as HttpContext ?? accessor.HttpContext) is { } http
                    && http.Request.Headers["Test-Grants"].ToString().Split(',').Contains(grant)));
            }
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IModuleIdentity, HeaderIdentity>();
        builder.Services.AddScoped<AbstractCoreContext>(_ => _factory.CreateAuditedContext(AuditActor.Anonymous));
        builder.Services.AddPrivateMessagingServices();
        builder.Services.AddScoped<ReportStore>();
        builder.Services.AddScoped<CoreReportEvidenceQueries>();
        builder.Services.AddScoped<ReportEmailDispatcher>();
        builder.Services.AddScoped<IReportEvidenceSource, PrivateMessageReportEvidenceSource>();
        builder.Services.AddSingleton(Substitute.For<IModuleUsers>());
        builder.Services.AddSingleton(Substitute.For<IModuleEmailSender>());
        builder.Services.AddSingleton(Substitute.For<ISessionQueries>());
        builder.Services.AddSingleton(Substitute.For<ISessionCommands>());
        builder.Services.AddSingleton(Substitute.For<IAccessQueries>());
        builder.Services.AddSingleton(Substitute.For<IUserManagementQueries>());
        builder.Services.AddSingleton(Substitute.For<IWallOfShameCommands>());
        builder.Services.AddSingleton(AccessCatalog.Core);
        builder.Services.AddScoped<UserManagementGate>();
        builder.Services.AddOptions<CommentsConfiguration>();
        _application = builder.Build();
        _application.UseRouting();
        _application.UseAuthentication();
        _application.UseAuthorization();
        _application.MapPrivateMessageEndpoints();
        _application.MapReportEndpoints();
        await _application.StartAsync();
        var address = _application.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        _client = new HttpClient { BaseAddress = new Uri(address) };
        As(_sender);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _application.DisposeAsync();
        _factory.Dispose();
    }

    private void As(Guid user, string grants = "PrivateMessage.Read,PrivateMessage.Write,PrivateMessage.Delete,Report.Write")
    {
        _client.DefaultRequestHeaders.Remove("Test-User");
        _client.DefaultRequestHeaders.Add("Test-User", user.ToString());
        _client.DefaultRequestHeaders.Remove("Test-Grants");
        _client.DefaultRequestHeaders.Add("Test-Grants", grants);
    }

    private async Task<Guid> Compose()
    {
        var response = await _client.PostAsJsonAsync("/private-messages", new
        {
            subject = "Private subject", content = "[b]Private content[/b]", userIds = new[] { _recipient }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        return (await response.Content.ReadFromJsonAsync<JsonNode>())!["id"]!.GetValue<Guid>();
    }

    [Fact]
    public async Task MailboxesPageMarkReadAndDeleteWithoutChangingOtherUsers()
    {
        var id = await Compose();
        As(_recipient);
        var inbox = (await _client.GetFromJsonAsync<JsonNode>("/private-messages?folder=inbox&pageSize=1"))!;
        Assert.Equal(1, inbox["total"]!.GetValue<int>());
        Assert.Equal(1, inbox["pageSize"]!.GetValue<int>());
        Assert.Equal(id, inbox["items"]![0]!["id"]!.GetValue<Guid>());
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/private-messages/{id}/read", null)).StatusCode);
        Assert.Equal(1, (await _client.GetFromJsonAsync<JsonNode>("/private-messages?folder=read"))!["total"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/private-messages/{id}/delete", null)).StatusCode);
        Assert.Equal(1, (await _client.GetFromJsonAsync<JsonNode>("/private-messages?folder=deleted"))!["total"]!.GetValue<int>());
        As(_sender);
        Assert.Equal(1, (await _client.GetFromJsonAsync<JsonNode>("/private-messages?folder=sent"))!["total"]!.GetValue<int>());
        As(_recipient);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/private-messages/{id}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task UnrelatedAdministratorsCannotReadOrReportPrivateEvidence()
    {
        var id = await Compose();
        As(_recipient);
        var detail = (await _client.GetFromJsonAsync<JsonNode>($"/private-messages/{id}"))!;
        var messageId = detail["messages"]![0]!["id"]!.GetValue<Guid>();
        As(_outsider, "PrivateMessage.Read,PrivateMessage.Write,PrivateMessageModeration.Manage,Report.Write,Report.Manage");
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/private-messages/{id}")).StatusCode);
        var report = await _client.PostAsJsonAsync("/reports", new
        {
            userIds = new[] { _sender }, reason = "Misconduct", sourceType = "privateMessage", sourceId = messageId
        });
        Assert.Equal(HttpStatusCode.NotFound, report.StatusCode);
    }

    [Fact]
    public async Task ReportSnapshotsAccessiblePrivateEvidenceAndDoesNotDiscloseItToReportedUser()
    {
        var id = await Compose();
        As(_recipient);
        var detail = (await _client.GetFromJsonAsync<JsonNode>($"/private-messages/{id}"))!;
        var messageId = detail["messages"]![0]!["id"]!.GetValue<Guid>();
        var submitted = await _client.PostAsJsonAsync("/reports", new
        {
            reason = "Misconduct", userIds = Array.Empty<Guid>(), sourceType = "privateMessage", sourceId = messageId
        });
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        var body = (await submitted.Content.ReadFromJsonAsync<JsonNode>())!;
        Assert.Null(body["reportedUsers"]);
        Assert.Null(body["evidence"]);
        var reportId = body["id"]!.GetValue<Guid>();
        var conversationId = body["conversationId"]!.GetValue<Guid>();
        As(_sender);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/private-messages/{conversationId}")).StatusCode);
        As(_outsider, "PrivateMessage.Read,Report.Manage");
        var report = (await _client.GetFromJsonAsync<JsonNode>($"/reports/{reportId}"))!;
        Assert.Equal("[b]Private content[/b]", report["evidence"]![0]!["content"]!.GetValue<string>());
        Assert.Equal(_sender, report["reportedUsers"]![0]!["id"]!.GetValue<Guid>());
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/private-messages/{conversationId}")).StatusCode);
    }

    [Fact]
    public async Task RoleRecipientsRequireModerationPermissionForCompositionAndInvitations()
    {
        var roleId = Guid.NewGuid();
        await using (var context = _factory.CreateContext())
        {
            var role = new UserRole("Message recipients");
            context.Roles.Add(role);
            context.Entry(role).Property<Guid>("Id").CurrentValue = roleId;
            await context.SaveChangesAsync();
        }
        var id = await Compose();
        foreach (var hidden in new[] { false, true })
        {
            var body = new { subject = "Chat", content = "Body", userIds = new[] { _recipient },
                roleIds = hidden ? Array.Empty<Guid>() : new[] { roleId },
                bccRoleIds = hidden ? new[] { roleId } : Array.Empty<Guid>(), isGroup = false };
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync("/private-messages", body)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _client.PostAsJsonAsync($"/private-messages/{id}/participants", body)).StatusCode);
        }
        Assert.Empty((await _client.GetFromJsonAsync<JsonNode>("/private-messages/recipients"))!["roles"]!.AsArray());
        As(_sender, "PrivateMessage.Read,PrivateMessage.Write,PrivateMessageModeration.Manage");
        Assert.Contains((await _client.GetFromJsonAsync<JsonNode>("/private-messages/recipients"))!["roles"]!.AsArray(),
            role => role!["id"]!.GetValue<Guid>() == roleId);
        Assert.Equal(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/private-messages", new
        {
            subject = "Chat", content = "Body", userIds = new[] { _recipient }, roleIds = new[] { roleId }, isGroup = false
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync($"/private-messages/{id}/participants", new { bccRoleIds = new[] { roleId } })).StatusCode);
    }

    [Fact]
    public async Task RecipientBrowsingAndSearchExcludeNonhumanAccountsAndTheWriter()
    {
        await using (var context = _factory.CreateContext())
        {
            await context.EnsureBuiltInUsersAsync();
        }
        As(_sender, "Report.Write");
        foreach (var reportTargets in new[] { false, true })
        {
            foreach (var search in new[] { "", "System", "Guest", "Test" })
            {
                var found = (await _client.GetFromJsonAsync<JsonNode>(
                    $"/private-messages/recipients?reportTargets={reportTargets}&search=" + search))!;
                var ids = found["users"]!.AsArray().Select(user => user!["id"]!.GetValue<Guid>()).ToArray();
                Assert.DoesNotContain(DomainConstants.SystemUserId, ids);
                Assert.DoesNotContain(DomainConstants.GuestUserId, ids);
                Assert.DoesNotContain(_sender, ids);
                Assert.Empty(found["roles"]!.AsArray());
                if (search is "" or "Test")
                {
                    Assert.Contains(_recipient, ids);
                    Assert.Contains(_outsider, ids);
                }
            }
        }
        As(_recipient);
        var recipients = (await _client.GetFromJsonAsync<JsonNode>("/private-messages/recipients"))!;
        var recipientIds = recipients["users"]!.AsArray().Select(user => user!["id"]!.GetValue<Guid>()).ToArray();
        Assert.DoesNotContain(_recipient, recipientIds);
        Assert.Contains(_sender, recipientIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportsRejectNonhumanTargetsEvenWhenTheyExistInTheHumanUserTable(bool system)
    {
        await using (var context = _factory.CreateContext())
        {
            await context.EnsureBuiltInUsersAsync();
        }
        var target = system ? DomainConstants.SystemUserId : DomainConstants.GuestUserId;
        var response = await _client.PostAsJsonAsync("/reports", new
        {
            userIds = new[] { _recipient, target }, reason = "Reason"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalidReportTargets", (await response.Content.ReadFromJsonAsync<JsonNode>())!["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task BlockedUsersHaveStablePrivatePagingAndShrinkAfterUnblocking()
    {
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/private-messages/blocks/{_recipient}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/private-messages/blocks/{_outsider}", null)).StatusCode);
        var firstResponse = await _client.GetAsync("/private-messages/blocks?page=1&pageSize=1");
        Assert.Contains("no-store", firstResponse.Headers.CacheControl!.ToString());
        var first = (await firstResponse.Content.ReadFromJsonAsync<JsonNode>())!;
        var second = (await _client.GetFromJsonAsync<JsonNode>("/private-messages/blocks?page=2&pageSize=1"))!;
        Assert.Equal(2, first["total"]!.GetValue<int>());
        Assert.Equal(2, first["pageCount"]!.GetValue<int>());
        Assert.Equal(1, first["page"]!.GetValue<int>());
        Assert.Equal(1, first["pageSize"]!.GetValue<int>());
        Assert.Equal(2, second["page"]!.GetValue<int>());
        var firstId = Assert.Single(first["items"]!.AsArray())!["id"]!.GetValue<Guid>();
        var secondId = Assert.Single(second["items"]!.AsArray())!["id"]!.GetValue<Guid>();
        Assert.NotEqual(firstId, secondId);
        Assert.Contains(firstId, new[] { _recipient, _outsider });
        Assert.Contains(secondId, new[] { _recipient, _outsider });
        var repeated = (await _client.GetFromJsonAsync<JsonNode>("/private-messages/blocks?page=1&pageSize=1"))!;
        Assert.Equal(firstId, repeated["items"]![0]!["id"]!.GetValue<Guid>());
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsync($"/private-messages/blocks/{secondId}/delete", null)).StatusCode);
        var shrunk = (await _client.GetFromJsonAsync<JsonNode>("/private-messages/blocks?page=2&pageSize=1"))!;
        Assert.Equal(1, shrunk["total"]!.GetValue<int>());
        Assert.Equal(1, shrunk["pageCount"]!.GetValue<int>());
        Assert.Empty(shrunk["items"]!.AsArray());
        As(_recipient);
        Assert.Equal(0, (await _client.GetFromJsonAsync<JsonNode>("/private-messages/blocks"))!["total"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("open", false, false)]
    [InlineData("resolved", true, false)]
    [InlineData("dismissed", true, false)]
    [InlineData("resolved", true, true)]
    public async Task ReportDetailsIdentifyTheHandlingAdministrator(string status, bool handled, bool missingProfile)
    {
        var id = Guid.NewGuid();
        await using (var context = _factory.CreateAuditedContext(AuditActor.Anonymous))
        {
            var conversationId = await new PrivateMessageStore(context).CreateReport(_sender, "Report", "Reason");
            await new ReportStore(context).Save(new(new(id, 1), _sender, conversationId, _recipient.ToString(),
                "Reason", "", Guid.Empty, null, "", status, handled ? _outsider : null, "", "", ""), default);
        }
        var users = _application.Services.GetRequiredService<IModuleUsers>();
        users.Find(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(missingProfile ? Array.Empty<IModuleUsers.Profile>() : new[] { new IModuleUsers.Profile(_outsider, "Administrator#1234", null) });
        As(_outsider, "Report.Manage");
        var detail = (await _client.GetFromJsonAsync<JsonNode>($"/reports/{id}"))!;
        if (handled)
        {
            Assert.Equal(_outsider, detail["resolvedById"]!.GetValue<Guid>());
            Assert.Equal(missingProfile ? _outsider.ToUserLabel(null) : "Administrator#1234",
                detail["resolvedByName"]!.GetValue<string>());
            await users.Received().Find(Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(_outsider)), Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.Null(detail["resolvedById"]);
            Assert.Null(detail["resolvedByName"]);
        }
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=1000001")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task InvalidBlockedUserPagingIsRejected(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/private-messages/blocks?" + query)).StatusCode);
    }

    [Fact]
    public async Task InvalidPagingAndMissingWriteGrantAreRejected()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/private-messages?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync("/private-messages?pageSize=101")).StatusCode);
        As(_sender, "PrivateMessage.Read");
        var response = await _client.PostAsJsonAsync("/private-messages", new
        {
            subject = "Subject", content = "Content", userIds = new[] { _recipient }
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
