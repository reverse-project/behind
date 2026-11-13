using System.Net.Mail;
using System.Security.Claims;
using Behind.Common;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Domain;
using Behind.Core.WebApi;
using Behind.Core.WebApi.Endpoints;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Tests.CoreWebApi;

public sealed class ReportResolutionTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BanResolutionIsAtomicProtectsDevelopersAndPersistsFailedNotifications(bool developerTarget)
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var reporter = await DatabaseSeedHelper.SeedHumanUserAsync(context, "reporter@example.test");
        var target = await DatabaseSeedHelper.SeedHumanUserAsync(context, "reported@example.test");
        var actor = await DatabaseSeedHelper.SeedHumanUserAsync(context, "moderator@example.test");
        var email = new Email("confirmed@example.test", DateTimeOffset.UtcNow);
        context.Add(email).Property<Guid>("UserId").CurrentValue = target;
        await context.SaveChangesAsync();
        var conversations = new PrivateMessageStore(context);
        var conversationId = await conversations.CreateReport(reporter, "User report", "Private reason");
        var reports = new ReportStore(context);
        var id = Guid.NewGuid();
        await reports.Save(new(new(id, 1), reporter, conversationId, target.ToString(), "Private reason",
            "", Guid.Empty, null, "", "open", null, "", "", ""), default);
        var sessionId = Guid.NewGuid();
        var sessionQueries = Substitute.For<ISessionQueries>();
        sessionQueries.TryFindByIdWithUserId(sessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((new ValidSession(
                AccessToken.Create(new GuestUser(None)), RefreshToken.Create(new GuestUser(None)),
                "127.0.0.1", "test"), actor)));
        var access = Substitute.For<IAccessQueries>();
        access.GetUserAccess(actor, Arg.Any<CancellationToken>())
            .Returns(new UserAccess([(Role)BuiltInAccess.Administrators.Role], [], []));
        var users = Substitute.For<IUserManagementQueries>();
        users.TryFindUser(target, Behind.Common.Database.DeletionState.Active, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(Some(new IUserManagementQueries.ManagedUser(target, "Reported", ["confirmed@example.test"],
                developerTarget ? [DomainConstants.DevelopersRole] : [DomainConstants.UsersRole], None)));
        var gate = new UserManagementGate(sessionQueries, access, users, AccessCatalog.Core, TimeProvider.System,
            Substitute.For<ILogger<UserManagementGate>>());
        var sessions = Substitute.For<ISessionCommands>();
        var sender = Substitute.For<Behind.Common.WebApi.IModuleEmailSender>();
        sender.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new SmtpException("Unavailable")));
        var dispatcher = new ReportEmailDispatcher(context, reports, sender, TimeProvider.System,
            Substitute.For<ILogger<ReportEmailDispatcher>>());
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", sessionId.ToString())], "test"))
        };
        var until = DateTimeOffset.UtcNow.AddDays(1);
        var result = await ReportEndpoints.Resolve(id, new("ban", until, "Thank you", "Please follow the rules"),
            http, gate, users, new WallOfShameCommands(context), sessions, context, reports, conversations, dispatcher);
        if (developerTarget)
        {
            Assert.IsType<ForbidHttpResult>(result);
        }
        else
        {
            Assert.Equal(200, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        }
        var report = (await reports.Find(id, false, default))!;
        if (developerTarget)
        {
            Assert.Equal("open", report.Status);
            Assert.Empty(await reports.Deliveries(id, default));
            Assert.False((await new WallOfShameQueries(context).FindActiveBan(target, DateTimeOffset.UtcNow)).Match(() => false, _ => true));
            await sessions.DidNotReceive().InvalidateAllSessions(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        }
        else
        {
            Assert.Equal("resolved", report.Status);
            Assert.Equal(actor, report.ResolvedById);
            Assert.Equal("Thank you", report.ReporterMessage);
            Assert.Equal("failed", Assert.Single(await reports.Deliveries(id, default)).Status);
            var ban = await new WallOfShameQueries(context).FindActiveBan(target, DateTimeOffset.UtcNow);
            Assert.True(ban.Match(() => false, active => active.Expires.Match(() => false, expires => expires == until)));
            Assert.Equal("Misconduct", ban.Match(() => "", active => active.Reason));
            await sessions.Received(1).InvalidateAllSessions(target, actor, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
            Assert.Equal(2, (await conversations.Find(reporter, conversationId))!.Messages.Count);
            var repeated = await ReportEndpoints.Resolve(id, new("ban", until, null, null),
                http, gate, users, new WallOfShameCommands(context), sessions, context, reports, conversations, dispatcher);
            Assert.Equal(409, Assert.IsAssignableFrom<IStatusCodeHttpResult>(repeated).StatusCode);
        }
    }
}
