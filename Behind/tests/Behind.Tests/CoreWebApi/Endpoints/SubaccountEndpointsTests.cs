using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Behind.Common.Database;
using Behind.Common.Database.Modules;
using Behind.Core.Common;
using Behind.Core.Common.Configuration;
using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Commands;
using Behind.Core.Database.Queries;
using Behind.Core.Database.Postgres;
using Behind.Core.WebApi.Endpoints;
using Behind.Core.WebApi.Identity;
using Behind.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Microsoft.Extensions.DependencyInjection;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SubaccountEndpointsTests
{
    private static (DefaultHttpContext Http, ISessionQueries Queries, Guid SessionId) AsUser(Guid actor, Guid? familyPrimaryId = null,
        bool impersonated = false)
    {
        var sessionId = Guid.NewGuid();
        var queries = Substitute.For<ISessionQueries>();
        queries.TryFindByIdWithUserId(sessionId, Arg.Any<CancellationToken>())
            .Returns(Some<(Session, Guid)>((new ValidSession(AccessToken.Create(new GuestUser(None)),
                RefreshToken.Create(new GuestUser(None)), "127.0.0.1", "test")
            {
                FamilyPrimaryUserId = familyPrimaryId.HasValue ? Some(familyPrimaryId.Value) : None,
                Impersonator = impersonated ? Some<User>(new GuestUser(None)) : None
            }, actor)));
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("session_id", sessionId.ToString())], "test"))
        };
        http.Request.Headers.UserAgent = "test";
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        return (http, queries, sessionId);
    }

    internal static async Task<SubaccountEndpoints.PinProof> SetPassword(AbstractCoreContext db, Guid actor, HttpContext http)
    {
        const string password = "test-family-passphrase";
        var settings = Substitute.For<IOptionsSnapshot<AccountsConfiguration>>();
        settings.Value.Returns(new AccountsConfiguration());
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IOptionsSnapshot<AccountsConfiguration>)).Returns(settings);
        http.RequestServices = services;
        await new PasswordCommands(db).CreateFromUserId(actor, new Password(PasswordHash.Create(password), true));
        await db.SaveChangesAsync();
        return new(Password: password);
    }

    [Fact]
    public async Task SwitchedAdultManagesOnlyChildrenUsingItsOwnProof()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var primary = new HumanUser("Primary", [new Email("primary@example.test")], None);
        var adult = new HumanUser("Adult", [new Email("adult@example.test")], None);
        var child = new HumanUser("Child", [new Email("child@example.test")], None);
        var otherAdult = new HumanUser("Other adult", [new Email("other@example.test")], None);
        var candidate = new HumanUser("Candidate", [new Email("candidate@example.test")], None);
        db.Users.AddRange(primary, adult, child, otherAdult, candidate);
        db.Subaccounts.AddRange(new Subaccount(primary, adult, false), new Subaccount(primary, child, true),
            new Subaccount(primary, otherAdult, false));
        db.UserSwitchPins.AddRange(new UserSwitchPin(adult, new PinHash(PasswordHash.Create("1111"))),
            new UserSwitchPin(child, new PinHash(PasswordHash.Create("2222"))));
        await db.SaveChangesAsync();
        var primaryId = db.GetKey(primary);
        var adultId = db.GetKey(adult);
        var childId = db.GetKey(child);
        var actor = AsUser(adultId, primaryId);
        var direct = AsUser(adultId);
        var security = Options.Create(new SecurityConfiguration { Accounts = new() { LinkUnlink = true }, EmailConfirmation = false });

        var payload = JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(
            await SubaccountEndpoints.List(actor.Http, actor.Queries, db, security)).Value);
        Assert.True(payload.GetProperty("CanManageChildren").GetBoolean());
        Assert.Equal(childId, Assert.Single(payload.GetProperty("Accounts").EnumerateArray()).GetProperty("UserId").GetGuid());
        var directPayload = JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(
            await SubaccountEndpoints.List(direct.Http, direct.Queries, db, security)).Value);
        Assert.False(directPayload.GetProperty("CanManageChildren").GetBoolean());
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Update(childId,
            new("Denied", null, true, null, false), direct.Http, direct.Queries, db, TimeProvider.System));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Update(db.GetKey(otherAdult),
            new("Denied", null, true, null, false), actor.Http, actor.Queries, db, TimeProvider.System));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Update(childId,
            new("Denied", null, false, null, false), actor.Http, actor.Queries, db, TimeProvider.System));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.ChangePin(childId,
            new("3333", false, new(CurrentPin: "2222")), actor.Http, actor.Queries, db));
        Assert.IsType<NoContent>(await SubaccountEndpoints.ChangePin(childId,
            new("3333", false, new(PrimaryPin: "1111")), actor.Http, actor.Queries, db));
        Assert.IsType<NoContent>(await SubaccountEndpoints.Update(childId,
            new("Updated child", null, true, null, false), actor.Http, actor.Queries, db, TimeProvider.System));
        var roles = Substitute.For<IRoleCommands>();
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Create(
            new("Adult", null, null, false, null), actor.Http, actor.Queries, db, roles, TimeProvider.System));
        var created = Assert.IsAssignableFrom<IStatusCodeHttpResult>(await SubaccountEndpoints.Create(
            new("New child", null, null, true, "4444", Proof: new(CurrentPin: "1111")),
            actor.Http, actor.Queries, db, roles, TimeProvider.System));
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(db.GetKey(candidate), actor.Http, actor.Queries, db));
        Assert.Equal(primaryId, candidate.PendingParentId);
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Release(db.GetKey(otherAdult),
            actor.Http, actor.Queries, db, TimeProvider.System, security));

        db.AppendSubaccountVersion(db.Subaccounts.Local.Single(link => link.User == adult), isChild: true);
        await db.SaveChangesAsync();
        var revoked = JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(
            await SubaccountEndpoints.List(actor.Http, actor.Queries, db, security)).Value);
        Assert.False(revoked.GetProperty("CanManageChildren").GetBoolean());
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Update(childId,
            new("Denied", null, true, null, false), actor.Http, actor.Queries, db, TimeProvider.System));
    }

    [Fact]
    public async Task PrimaryPinManagementRequiresProofAndFamilyAuthority()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parentId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@example.test");
        var outsiderId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "other@example.test");
        var parent = AsUser(parentId);
        var parentUser = (await db.Users.FindAsync(parentId))!;
        var childUser = (await db.Users.FindAsync(childId))!;
        db.Subaccounts.Add(new Subaccount(parentUser, childUser, false));
        db.UserSwitchPins.Add(new UserSwitchPin(childUser, new PinHash(PasswordHash.Create("1234"))));
        await db.SaveChangesAsync();
        var password = await SetPassword(db, parentId, parent.Http);
        var roles = Substitute.For<IRoleCommands>();
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Create(new("Rejected", null, null, false, "123", null),
            parent.Http, parent.Queries, db, roles, TimeProvider.System));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Create(new("Rejected", null, null, false, null, "321"),
            parent.Http, parent.Queries, db, roles, TimeProvider.System));
        var edit = new SubaccountEndpoints.UpdateRequest("Rejected", null, true, "5555", false);
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Update(childId, edit, parent.Http, parent.Queries, db, TimeProvider.System));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Update(childId, edit with { Pin = null, ClearPin = true },
            parent.Http, parent.Queries, db, TimeProvider.System));
        Assert.NotEqual("Rejected", childUser.DisplayName);

        var change = new SubaccountEndpoints.PinRequest("5678", false, new(CurrentPin: "1234"));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.ChangePin(parentId, change, parent.Http, parent.Queries, db));
        Assert.IsType<NotFound>(await SubaccountEndpoints.ChangePin(outsiderId, change, parent.Http, parent.Queries, db));
        Assert.IsType<NoContent>(await SubaccountEndpoints.ChangePin(childId, change, parent.Http, parent.Queries, db));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.ChangePin(childId, change, parent.Http, parent.Queries, db));
        var remove = new SubaccountEndpoints.PinRequest(null, true, new(Password: "wrong"));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.ChangePin(childId, remove, parent.Http, parent.Queries, db));
        Assert.IsType<NoContent>(await SubaccountEndpoints.ChangePin(childId, remove with { Proof = password }, parent.Http, parent.Queries, db));
        Assert.False(await db.UserSwitchPins.AnyAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SubaccountCanSetChangeAndUnsetOnlyItsOwnPin(bool switched)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parentId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@example.test");
        var siblingId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "sibling@example.test");
        var parent = (await db.Users.FindAsync(parentId))!;
        var child = (await db.Users.FindAsync(childId))!;
        var sibling = (await db.Users.FindAsync(siblingId))!;
        db.Subaccounts.AddRange(new Subaccount(parent, child, true), new Subaccount(parent, sibling, false));
        db.UserSwitchPins.AddRange(
            new UserSwitchPin(parent, new PinHash(PasswordHash.Create("1234"))),
            new UserSwitchPin(sibling, new PinHash(PasswordHash.Create("1234"))));
        await db.SaveChangesAsync();
        var actor = AsUser(childId, switched ? parentId : null);
        var password = await SetPassword(db, childId, actor.Http);
        var currentPin = new SubaccountEndpoints.PinProof(CurrentPin: "1234");

        Task<IResult> Change(Guid target, string? pin, SubaccountEndpoints.PinProof? proof) =>
            SubaccountEndpoints.ChangePin(target, new(pin, pin is null, proof), actor.Http, actor.Queries, db);

        async Task<bool> CurrentHasPin()
        {
            var result = await SubaccountEndpoints.List(actor.Http, actor.Queries, db, Options.Create(new SecurityConfiguration()));
            var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
            return JsonSerializer.SerializeToElement(value).GetProperty("CurrentHasPin").GetBoolean();
        }

        Assert.False(await CurrentHasPin());
        Assert.IsType<ForbidHttpResult>(await Change(parentId, "5678", null));
        Assert.IsType<ForbidHttpResult>(await Change(siblingId, "5678", null));
        Assert.IsType<ForbidHttpResult>(await Change(childId, null, null));
        Assert.IsType<ForbidHttpResult>(await Change(childId, "1234", currentPin));
        Assert.IsType<NoContent>(await Change(childId, "1234", null));
        Assert.True(await CurrentHasPin());
        Assert.IsType<ForbidHttpResult>(await Change(parentId, "5678", currentPin));
        Assert.IsType<ForbidHttpResult>(await Change(siblingId, null, currentPin));
        Assert.IsType<ForbidHttpResult>(await Change(childId, "5678", null));
        Assert.IsType<ForbidHttpResult>(await Change(childId, null, null));
        Assert.IsType<ForbidHttpResult>(await Change(childId, "5678", new(CurrentPin: "0000")));
        Assert.IsType<ForbidHttpResult>(await Change(childId, null, new(CurrentPin: "0000")));
        Assert.IsType<ForbidHttpResult>(await Change(childId, "5678", password));
        Assert.IsType<ForbidHttpResult>(await Change(childId, "5678", new(PrimaryPin: "1234")));
        Assert.IsType<ForbidHttpResult>(await Change(childId, "5678", new(MagicToken: "token")));
        Assert.IsType<ForbidHttpResult>(await Change(childId, "5678", currentPin with { Password = password.Password }));

        Assert.IsType<NoContent>(await Change(childId, "5678", currentPin));
        Assert.IsType<ForbidHttpResult>(await Change(childId, null, currentPin));
        Assert.IsType<NoContent>(await Change(childId, null, new(CurrentPin: "5678")));
        Assert.False(await CurrentHasPin());
        Assert.IsType<ForbidHttpResult>(await Change(childId, "1234", new(CurrentPin: "5678")));
        Assert.IsType<ForbidHttpResult>(await Change(childId, null, new(CurrentPin: "5678")));
        Assert.Equal(2, await db.UserSwitchPins.CountAsync());
        Assert.All(await db.UserSwitchPins.ToListAsync(),
            pin => Assert.True(PasswordHash.CreateFromExisting(pin.PinHash.ToString()).Verify("1234")));
        Assert.IsType<NoContent>(await Change(childId, "9012", null));
        Assert.True(await CurrentHasPin());
        Assert.IsType<ForbidHttpResult>(await Change(childId, "3456", null));
        Assert.IsType<NoContent>(await Change(childId, null, new(CurrentPin: "9012")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PrimaryPinProofChangesAndRemovesOnlyLinkedSubaccountPins(bool useProfileUpdate)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parentId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@example.test");
        var outsiderId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "other@example.test");
        var parent = AsUser(parentId);
        var child = AsUser(childId);
        var parentUser = (await db.Users.FindAsync(parentId))!;
        var childUser = (await db.Users.FindAsync(childId))!;
        db.Subaccounts.Add(new Subaccount(parentUser, childUser, false));
        db.UserSwitchPins.Add(new UserSwitchPin(parentUser, new PinHash(PasswordHash.Create("1234"))));
        await db.SaveChangesAsync();

        Task<IResult> Mutate(Guid target, (DefaultHttpContext Http, ISessionQueries Queries, Guid SessionId) actor,
            string? pin, SubaccountEndpoints.PinProof proof) => useProfileUpdate
            ? SubaccountEndpoints.Update(target, new("Updated", null, false, pin, pin is null, proof),
                actor.Http, actor.Queries, db, TimeProvider.System)
            : SubaccountEndpoints.ChangePin(target, new(pin, pin is null, proof), actor.Http, actor.Queries, db);

        var primaryProof = new SubaccountEndpoints.PinProof(PrimaryPin: "1234");
        Assert.IsType<NotFound>(await Mutate(outsiderId, parent, "5678", primaryProof));
        Assert.IsType<ForbidHttpResult>(await Mutate(childId, child, "5678", primaryProof));
        Assert.IsType<ForbidHttpResult>(await Mutate(childId, parent, "5678", new(CurrentPin: "1234")));
        Assert.IsType<ForbidHttpResult>(await Mutate(childId, parent, "5678", primaryProof with { Password = "other proof" }));
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.ChangePin(parentId, new("5678", false, primaryProof),
            parent.Http, parent.Queries, db));
        Assert.IsType<NoContent>(await Mutate(childId, parent, "5678", primaryProof));
        var updated = await db.UserSwitchPins.SingleAsync(pin => EF.Property<Guid>(pin, "UserId") == childId);
        Assert.True(PasswordHash.CreateFromExisting(updated.PinHash.ToString()).Verify("5678"));
        Assert.IsType<ForbidHttpResult>(await Mutate(childId, parent, null, new(PrimaryPin: "5678")));
        Assert.IsType<NoContent>(await Mutate(childId, parent, null, primaryProof));
        var remaining = await db.UserSwitchPins.SingleAsync();
        Assert.Equal(parentId, db.Entry(remaining).Property<Guid>("UserId").CurrentValue);
        Assert.True(PasswordHash.CreateFromExisting(remaining.PinHash.ToString()).Verify("1234"));
    }

    [Fact]
    public async Task BrowsingPagesOnlyEligibleIndependentUsersWithPublicLabels()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var requester = AsUser(await DatabaseSeedHelper.SeedHumanUserAsync(db));
        var first = new HumanUser("Candidate A", [new Email("first@example.test")], None);
        var second = new HumanUser("Candidate B", [new Email("second@example.test")], None);
        var child = new HumanUser("Candidate child", [new Email("child@example.test")], None);
        var parent = new HumanUser("Candidate parent", [new Email("parent@example.test")], None);
        var emailLess = new HumanUser { DisplayName = "Candidate without email" };
        db.Users.AddRange(first, second, child, parent, emailLess);
        db.Subaccounts.Add(new Subaccount(parent, child, true));
        await db.SaveChangesAsync();

        async Task<Guid[]> Page(int page)
        {
            var response = await SubaccountLinkEndpoints.Candidates(" CANDIDATE ", requester.Http, requester.Queries, db, page, 1);
            var result = Assert.IsType<Ok<IEnumerable<SubaccountLinkEndpoints.Candidate>>>(response);
            var rows = result.Value!.ToArray();
            Assert.All(rows, row => Assert.DoesNotContain("@", row.DisplayName));
            return rows.Select(row => row.UserId).ToArray();
        }

        Assert.Equal([db.GetKey(first)], await Page(1));
        Assert.Equal([db.GetKey(second)], await Page(2));
        Assert.Equal([db.GetKey(child)], await Page(3));
        Assert.Empty(await Page(4));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(db.GetKey(first), requester.Http, requester.Queries, db));
        Assert.Equal([db.GetKey(second)], await Page(1));
        var existingParent = AsUser(db.GetKey(parent));
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.RequestLink(db.GetKey(child), existingParent.Http, existingParent.Queries, db));
    }

    [Theory]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, true)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, true, true, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, false, true, false, false)]
    public async Task FamilyListSeparatesBrowseAndReleaseFromInvitationPermissions(
        bool configured, bool isPrimary, bool switched, bool invitationsEnabled, bool invitationsRequireDirectSignIn)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parentId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@example.test");
        var candidateId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "candidate@example.test");
        var parent = (await db.Users.FindAsync(parentId))!;
        var child = (await db.Users.FindAsync(childId))!;
        db.Subaccounts.Add(new Subaccount(parent, child, true));
        await db.SaveChangesAsync();

        var actor = AsUser(isPrimary ? parentId : childId, switched ? parentId : null);
        var security = Options.Create(new SecurityConfiguration
        {
            Accounts = new() { LinkUnlink = configured },
            EmailConfirmation = false
        });
        var result = await SubaccountEndpoints.List(actor.Http, actor.Queries, db, security);
        var payload = JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        var canBrowseAndRelease = configured && isPrimary;
        Assert.Equal(canBrowseAndRelease, payload.GetProperty("CanBrowseAndRelease").GetBoolean());
        Assert.Equal(invitationsEnabled, payload.GetProperty("LinkUnlinkEnabled").GetBoolean());
        Assert.Equal(configured && !switched, payload.GetProperty("CanRespondToInvitations").GetBoolean());
        Assert.Equal(invitationsRequireDirectSignIn, payload.GetProperty("LinkUnlinkRequiresDirectSignIn").GetBoolean());
        if (isPrimary)
        {
            Assert.Equal(canBrowseAndRelease, payload.GetProperty("Accounts")[0].GetProperty("CanRelease").GetBoolean());
        }
        if (!configured)
        {
            return;
        }

        if (!invitationsEnabled)
        {
            Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.RequestLink(candidateId, actor.Http, actor.Queries, db));
        }
        if (switched)
        {
            Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.Requests(actor.Http, actor.Queries, db));
            Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.Confirm(Guid.NewGuid(), actor.Http, actor.Queries, db));
            Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.Decline(Guid.NewGuid(), actor.Http, actor.Queries, db));
        }
        var browse = await SubaccountLinkEndpoints.Candidates(null, actor.Http, actor.Queries, db);
        if (isPrimary)
        {
            var candidates = Assert.IsType<Ok<IEnumerable<SubaccountLinkEndpoints.Candidate>>>(browse);
            Assert.Equal(candidateId, Assert.Single(candidates.Value!).UserId);
            Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Release(candidateId, actor.Http, actor.Queries, db, TimeProvider.System, security));
            Assert.IsType<Conflict>(await SubaccountLinkEndpoints.Release(childId, actor.Http, actor.Queries, db, TimeProvider.System,
                Options.Create(security.Value with { EmailConfirmation = true })));
            Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Release(childId, actor.Http, actor.Queries, db, TimeProvider.System, security));
        }
        else
        {
            Assert.IsType<ForbidHttpResult>(browse);
            Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.Release(childId, actor.Http, actor.Queries, db, TimeProvider.System, security));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BrowseAndReleaseRejectImpersonationAndMismatchedFamily(bool impersonated)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parentId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var actor = AsUser(parentId, impersonated ? parentId : Guid.NewGuid(), impersonated);
        var security = Options.Create(new SecurityConfiguration { Accounts = new() { LinkUnlink = true } });

        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.List(actor.Http, actor.Queries, db, security));
        Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.Candidates(null, actor.Http, actor.Queries, db));
        Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.Release(Guid.NewGuid(),
            actor.Http, actor.Queries, db, TimeProvider.System, security));
    }

    [Fact]
    public async Task OnlyIntendedChildCanAcceptCurrentInvitationAndReleasePreservesAccount()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateAuditedContext(AuditActor.Anonymous);
        var parentId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@test.com");
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@test.com");
        var otherId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "other@test.com");
        var parent = AsUser(parentId);
        var child = AsUser(childId);
        var other = AsUser(otherId);
        var security = Options.Create(new SecurityConfiguration(false, None, None, None, EmailConfirmation: false));
        var requireConfirmed = Options.Create(new SecurityConfiguration());

        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var repository = db.GetRepository<HumanUser, Guid>();
        var updated = (await repository.TryFindAsync(childId)).GetOrElse(new HumanUser());
        repository.Update(childId, updated with { DisplayName = "Updated name" });
        await db.SaveChangesAsync();
        Assert.Empty(await db.Subaccounts.ToListAsync());
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.RequestLink(childId, other.Http, other.Queries, db));
        var pending = Assert.IsType<Ok<List<SubaccountLinkEndpoints.Invitation>>>(
            await SubaccountLinkEndpoints.Requests(child.Http, child.Queries, db));
        var invitation = Assert.Single(pending.Value!);
        Assert.Equal(parentId, invitation.UserId);
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Confirm(invitation.RequestId, parent.Http, parent.Queries, db));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Decline(invitation.RequestId, child.Http, child.Queries, db));
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Confirm(invitation.RequestId, child.Http, child.Queries, db));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var current = (await db.Users.FindAsync(childId))!.PendingLinkRequestId!.Value;
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Confirm(current, child.Http, child.Queries, db));
        Assert.IsType<ForbidHttpResult>(await SubaccountLinkEndpoints.RequestLink(otherId, child.Http, child.Queries, db));
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.RequestLink(parentId, other.Http, other.Queries, db));
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Release(childId, other.Http, other.Queries, db, TimeProvider.System, security));
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.Release(childId, parent.Http, parent.Queries, db, TimeProvider.System, requireConfirmed));
        var commands = new SessionCommands(db);
        var queries = new SessionQueries(db);
        var user = (await db.Users.FindAsync(childId))!;
        var switchedSession = await commands.CreateForUserId(childId,
            new ValidSession(AccessToken.Create(user), RefreshToken.Create(user), "127.0.0.1", "test") { FamilyPrimaryUserId = parentId });
        var directSession = await commands.CreateForUserId(childId,
            new ValidSession(AccessToken.Create(user), RefreshToken.Create(user), "127.0.0.1", "test"));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Release(childId, parent.Http, parent.Queries, db, TimeProvider.System, security));
        Assert.True(await queries.TryFindById(switchedSession) == None);
        Assert.True((await queries.TryFindById(directSession)).Match(() => false, session => session is ValidSession));
        Assert.False(await db.ActiveSubaccounts().AnyAsync());
        var released = await db.LatestSubaccounts().SingleAsync();
        Assert.False(released.IsActive);
        Assert.True(await db.Emails.AnyAsync(email => email.Value == "child@test.com"));
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Confirm(current, child.Http, child.Queries, db));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var next = (await db.Users.FindAsync(childId))!.PendingLinkRequestId!.Value;
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Confirm(next, child.Http, child.Queries, db));
        var restored = await db.LatestSubaccounts().SingleAsync();
        Assert.True(restored.IsActive);
        Assert.True((await queries.TryFindById(switchedSession)).Match(() => false, session => session is InvalidSession));
    }

    [Fact]
    public async Task AdultProfileCannotBeConvertedWhileAnotherAdultLinksItAsAChild()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var firstId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "first@example.test");
        var secondId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "second@example.test");
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@example.test");
        var first = (await db.Users.FindAsync(firstId))!;
        var second = (await db.Users.FindAsync(secondId))!;
        var child = (await db.Users.FindAsync(childId))!;
        var originalName = child.DisplayName;
        db.Subaccounts.AddRange(new Subaccount(first, child, true), new Subaccount(second, child, true));
        await db.SaveChangesAsync();

        var actor = AsUser(firstId);
        var result = await SubaccountEndpoints.Update(childId,
            new("Changed", null, false, null, false), actor.Http, actor.Queries, db, TimeProvider.System);

        Assert.IsType<Conflict>(result);
        Assert.Equal(originalName, child.DisplayName);
        Assert.Equal(2, await db.Subaccounts.CountAsync(link => link.IsChild));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FeatureGatesCheckBothFlagsForRelationshipOperations(bool subaccounts, bool linking)
    {
        var settings = new SecurityConfiguration { Accounts = new() { Subaccounts = subaccounts, LinkUnlink = linking } };
        using var services = new ServiceCollection().AddSingleton(Options.Create(settings)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var context = new DefaultEndpointFilterInvocationContext(http, []);
        foreach (var requiresLinking in new[] { false, true })
        {
            var result = await new SubaccountFeatureFilter(requiresLinking)
                .InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.NoContent()));
            Assert.Equal(subaccounts && (!requiresLinking || linking) ? 204 : 404,
                Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task DisabledFeatureLeavesPendingConsentUntouched(bool subaccounts, bool linking)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parent = AsUser(await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test"));
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@example.test");
        var child = AsUser(childId);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var pending = await db.Users.Select(user => user.PendingLinkRequestId).SingleAsync(id => id != null);
        var settings = new SecurityConfiguration { Accounts = new() { Subaccounts = subaccounts, LinkUnlink = linking } };
        using var services = new ServiceCollection().AddSingleton(Options.Create(settings)).BuildServiceProvider();
        child.Http.RequestServices = services;
        var invocation = new DefaultEndpointFilterInvocationContext(child.Http, []);
        var result = await new SubaccountFeatureFilter(true).InvokeAsync(invocation,
            async _ => await SubaccountLinkEndpoints.Confirm(pending!.Value, child.Http, child.Queries, db));
        Assert.IsType<NotFound>(result);
        Assert.False(await db.Subaccounts.AnyAsync());
        Assert.True(await db.Users.AnyAsync(user => user.PendingLinkRequestId == pending));
        using var enabled = new ServiceCollection().AddSingleton(Options.Create(new SecurityConfiguration
        {
            Accounts = new() { LinkUnlink = true }
        })).BuildServiceProvider();
        child.Http.RequestServices = enabled;
        Assert.IsType<NoContent>(await new SubaccountFeatureFilter(true).InvokeAsync(invocation,
            async _ => await SubaccountLinkEndpoints.Confirm(pending!.Value, child.Http, child.Queries, db)));
        Assert.True(await db.Subaccounts.AnyAsync(link => EF.Property<Guid>(link, "UserId") == childId));
        Assert.False(await db.Users.AnyAsync(user => user.PendingLinkRequestId == pending));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresOverlappingRequestSnapshotsCannotReplaceWinningParent()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres:Image is required.");
        await using var container = new PostgreSqlBuilder(image).Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(container.GetConnectionString()).Options;
        await using var setup = new CoreContextPostgres(options);
        await setup.Database.MigrateAsync();
        var first = new HumanUser("First", [new Email("first@test.com")], None);
        var second = new HumanUser("Second", [new Email("second@test.com")], None);
        var child = new HumanUser("Child", [new Email("child@test.com")], None);
        setup.Users.AddRange(first, second, child);
        await setup.SaveChangesAsync();
        var firstId = setup.GetKey(first);
        var childId = setup.GetKey(child);
        var winner = AsUser(firstId);
        var loser = AsUser(setup.GetKey(second));
        await using var left = new CoreContextPostgres(options);
        await using var right = new CoreContextPostgres(options);
        await using var firstTransaction = await left.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var staleTransaction = await right.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        Assert.Null((await right.Users.FindAsync(childId))!.PendingLinkRequestId);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, winner.Http, winner.Queries, left));
        await firstTransaction.CommitAsync();
        var error = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            SubaccountLinkEndpoints.RequestLink(childId, loser.Http, loser.Queries, right));
        Assert.Equal("40001", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
        await staleTransaction.RollbackAsync();
        setup.ChangeTracker.Clear();
        Assert.Equal(firstId, (await setup.Users.FindAsync(childId))!.PendingParentId);
        Assert.False(await setup.Subaccounts.AnyAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresReleasedFamilyLinkAndUnsetPinCanBeRestored()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres:Image is required.");
        await using var container = new PostgreSqlBuilder(image).Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>()
            .UseNpgsql(container.GetConnectionString())
            .AddInterceptors(new AuditSaveChangesInterceptor(
                () => new FixedAuditActorProvider(AuditActor.Anonymous), TimeProvider.System)).Options;
        await using var db = new CoreContextPostgres(options);
        await db.Database.MigrateAsync();
        var parentUser = new HumanUser("Parent", [new Email("parent@example.test")], None);
        var childUser = new HumanUser("Child", [new Email("child@example.test")], None);
        db.Users.AddRange(parentUser, childUser);
        await db.SaveChangesAsync();
        var parentId = db.GetKey(parentUser);
        var childId = db.GetKey(childUser);
        db.Subaccounts.Add(new Subaccount(parentUser, childUser, true));
        db.UserSwitchPins.Add(new UserSwitchPin(childUser, new PinHash(PasswordHash.Create("0042").ToString())));
        await db.SaveChangesAsync();

        var parent = AsUser(parentId);
        var child = AsUser(childId);
        var security = Options.Create(new SecurityConfiguration(false, None, None, None, EmailConfirmation: false));
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Release(
            childId, parent.Http, parent.Queries, db, TimeProvider.System, security));
        Assert.False(await db.ActiveSubaccounts().AnyAsync());
        Assert.False((await db.LatestSubaccounts().SingleAsync()).IsActive);

        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var requestId = (await db.Users.FindAsync(childId))!.PendingLinkRequestId!.Value;
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.Confirm(requestId, child.Http, child.Queries, db));
        Assert.True((await db.LatestSubaccounts().SingleAsync()).IsActive);

        await new UserCommands(db).UnsetSwitchPin(childId);
        Assert.False(await db.UserSwitchPins.AnyAsync());
        Assert.IsType<NoContent>(await SubaccountEndpoints.ChangePin(
            childId, new("5678", false), child.Http, child.Queries, db));
        var pin = await db.UserSwitchPins.SingleAsync();
        Assert.True(PasswordHash.CreateFromExisting(pin.PinHash.ToString()).Verify("5678"));
    }

    [Fact]
    public async Task EmailRemovedBeforeConsentPreventsLinkAndEmailLessChildCannotBeReleased()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var parentId = await DatabaseSeedHelper.SeedHumanUserAsync(db);
        var childId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "child@test.com");
        var parent = AsUser(parentId);
        var child = AsUser(childId);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(childId, parent.Http, parent.Queries, db));
        var user = (await db.Users.FindAsync(childId))!;
        var requestId = user.PendingLinkRequestId!.Value;
        db.Emails.Remove(await db.Emails.SingleAsync(address => address.Value == "child@test.com"));
        await db.SaveChangesAsync();
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.Confirm(requestId, child.Http, child.Queries, db));
        Assert.False(await db.Subaccounts.AnyAsync());
        db.Subaccounts.Add(new Subaccount((await db.Users.FindAsync(parentId))!, user, true));
        await db.SaveChangesAsync();
        var security = Options.Create(new SecurityConfiguration(false, None, None, None, EmailConfirmation: false));
        Assert.IsType<Conflict>(await SubaccountLinkEndpoints.Release(childId, parent.Http, parent.Queries, db, TimeProvider.System, security));
        Assert.True(await db.Subaccounts.AnyAsync());
    }

    [Fact]
    public async Task CreatingAChildDiscardsAnIncomingInvitation()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var proposer = AsUser(await DatabaseSeedHelper.SeedHumanUserAsync(db, "proposer@example.test"));
        var recipientId = await DatabaseSeedHelper.SeedHumanUserAsync(db, "recipient@example.test");
        var recipient = AsUser(recipientId);
        Assert.IsType<NoContent>(await SubaccountLinkEndpoints.RequestLink(recipientId, proposer.Http, proposer.Queries, db));
        var requestId = await db.Users.Select(user => user.PendingLinkRequestId).SingleAsync(id => id != null);
        var created = await SubaccountEndpoints.Create(new("New child", null, null, true, null),
            recipient.Http, recipient.Queries, db, Substitute.For<IRoleCommands>(), TimeProvider.System);
        Assert.Equal(201, Assert.IsAssignableFrom<IStatusCodeHttpResult>(created).StatusCode);
        Assert.IsType<NotFound>(await SubaccountLinkEndpoints.Confirm(requestId!.Value, recipient.Http, recipient.Queries, db));
    }

    [Fact]
    public async Task ConcurrentPrimaryPinSetupPreservesWinningPinAndRollsBackNewChild()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var primary = await DatabaseSeedHelper.SeedHumanUserAsync(db);
        var parent = AsUser(primary);
        var roles = Substitute.For<IRoleCommands>();
        var proof = await SetPassword(db, primary, parent.Http);
        var winningHash = PasswordHash.Create("000789").ToString();
        db.SavingChanges += (_, _) => db.Database.ExecuteSql(
            $"INSERT INTO UserSwitchPins (UserId, PinHash) VALUES ({primary}, {winningHash})");

        var response = await SubaccountEndpoints.Create(new("Child", null, null, true, "001234", "002345", proof),
            parent.Http, parent.Queries, db, roles, TimeProvider.System);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<IStatusCodeHttpResult>(response).StatusCode);
        await using var verified = factory.CreateContext();
        Assert.Equal(winningHash, (await verified.UserSwitchPins.SingleAsync()).PinHash.ToString());
        Assert.False(await verified.Subaccounts.AnyAsync());
        Assert.False(await verified.Users.AnyAsync(user => user.DisplayName == "Child"));
    }

    [Fact]
    public async Task SqliteConcurrentFirstChildPinUpdatePreservesWinningPinAndProfile()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        await VerifyConcurrentFirstChildPinUpdate(db, factory.CreateContext);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task PostgresConcurrentFirstChildPinUpdatePreservesWinningPinAndProfile()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "postgresql.testsettings.json"));
        var image = configuration["Postgres:Image"] ?? throw new InvalidOperationException("Postgres:Image is required.");
        await using var container = new PostgreSqlBuilder(image).Build();
        await container.StartAsync();
        var options = new DbContextOptionsBuilder<CoreContextPostgres>().UseNpgsql(container.GetConnectionString()).Options;
        await using var db = new CoreContextPostgres(options);
        await db.Database.MigrateAsync();
        await VerifyConcurrentFirstChildPinUpdate(db, () => new CoreContextPostgres(options));
    }

    private static async Task VerifyConcurrentFirstChildPinUpdate(AbstractCoreContext db, Func<AbstractCoreContext> createContext)
    {
        var primary = new HumanUser("Parent", [new Email("parent@example.test")], None);
        var child = new HumanUser("Child", [new Email("child@example.test")], None);
        db.Subaccounts.Add(new Subaccount(primary, child, true));
        await db.SaveChangesAsync();
        var parent = AsUser(db.GetKey(primary));
        var childId = db.GetKey(child);
        var proof = await SetPassword(db, db.GetKey(primary), parent.Http);
        var winningHash = PasswordHash.Create("000789").ToString();
        db.SavingChanges += (_, _) => db.Database.ExecuteSql(
            $"""INSERT INTO "UserSwitchPins" ("UserId", "PinHash") VALUES ({childId}, {winningHash})""");

        var response = await SubaccountEndpoints.Update(childId, new("Losing name", null, false, "001234", false, proof),
            parent.Http, parent.Queries, db, TimeProvider.System);

        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<IStatusCodeHttpResult>(response).StatusCode);
        await using var verified = createContext();
        Assert.Equal(winningHash, (await verified.UserSwitchPins.SingleAsync()).PinHash.ToString());
        Assert.Equal("Child", (await verified.Users.FindAsync(childId))!.DisplayName);
        Assert.True((await verified.ActiveSubaccounts().SingleAsync()).IsChild);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("000123", "child@example.test")]
    public async Task LinkedUserCanBeCreatedSwitchedAndManagedOnlyByPrimary(string? pin, string? email)
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var primary = await DatabaseSeedHelper.SeedHumanUserAsync(db, "parent@example.test");
        var parent = AsUser(primary);
        var roles = Substitute.For<IRoleCommands>();
        var birthday = new DateOnly(2018, 2, 2);
        var proof = await SetPassword(db, primary, parent.Http);
        var request = new SubaccountEndpoints.CreateRequest("Child", email, birthday, true, pin, Proof: proof);
        await SubaccountEndpoints.Create(request, parent.Http, parent.Queries, db, roles, TimeProvider.System);
        db.ChangeTracker.Clear();
        var link = await db.Subaccounts
            .Include(account => account.PrimaryUser)
            .Include(account => account.User)
            .SingleAsync();
        var childId = db.GetKey(link.User);
        Assert.Equal(primary, db.GetKey(link.PrimaryUser));
        Assert.NotEqual(primary, childId);
        var credential = await db.UserSwitchPins.SingleOrDefaultAsync();
        Assert.Equal(pin is not null, credential is not null);
        if (pin is not null)
        {
            var storedHash = Assert.IsType<PinHash>(credential!.PinHash);
            Assert.NotEqual(pin, storedHash.ToString());
            Assert.True(PasswordHash.CreateFromExisting(storedHash.ToString()).Verify(pin));
        }
        var profiles = new UserProfiles(db, Substitute.For<IModuleFiles>());
        await profiles.SetDetails(childId, new(new DateOnly(1980, 1, 1), false), default);
        Assert.Equal(new UserProfiles.Details(birthday, false, true), await profiles.GetDetails(childId, default));
        await SubaccountEndpoints.List(parent.Http, parent.Queries, db, Options.Create(new SecurityConfiguration()));

        var child = AsUser(childId);
        Assert.IsType<ForbidHttpResult>(await SubaccountEndpoints.Create(request, child.Http, child.Queries, db, roles, TimeProvider.System));
        var other = AsUser(await DatabaseSeedHelper.SeedHumanUserAsync(db, "elsewhere@example.test"));
        var update = new SubaccountEndpoints.UpdateRequest("Different", birthday, false, null, true, proof);
        Assert.IsType<NotFound>(await SubaccountEndpoints.Update(childId, update, other.Http, other.Queries, db, TimeProvider.System));

        var manager = Substitute.For<UserManager<ApplicationUser>>(
            Substitute.For<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);
        manager.FindByIdAsync(childId.ToString()).Returns(new ApplicationUser { Id = childId });
        var security = Options.Create(new SecurityConfiguration(false, None, Some(new JwtSettings("issuer", "audience", new string('k', 64))), None));
        var commands = Substitute.For<ISessionCommands>();
        Task<IResult> Switch(string? suppliedPin) => SubaccountEndpoints.Switch(childId, new(suppliedPin),
            parent.Http, parent.Queries, db, manager, security, commands, Substitute.For<IWallOfShameQueries>(),
            TimeProvider.System, NullLogger<SubaccountEndpoints>.Instance);

        if (pin is not null)
        {
            Assert.IsType<ForbidHttpResult>(await Switch(null));
            Assert.IsType<ForbidHttpResult>(await Switch("000000"));
            await commands.DidNotReceiveWithAnyArgs().CreateForUserId(default, default!, default);
            Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(pin));
        }
        else
        {
            Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(null));
        }
        await commands.Received(1).CreateForUserId(childId, Arg.Is<ValidSession>(s => s.FamilyPrimaryUserId == primary), Arg.Any<CancellationToken>());
        await commands.Received(1).InvalidateSession(parent.SessionId, primary, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        Assert.IsType<NoContent>(await SubaccountEndpoints.Update(childId, update, parent.Http, parent.Queries, db, TimeProvider.System));
        Assert.False(await db.UserSwitchPins.AnyAsync());
        Assert.IsType<JsonHttpResult<SignInEndpoints.SignInResponse>>(await Switch(null));
    }
}
