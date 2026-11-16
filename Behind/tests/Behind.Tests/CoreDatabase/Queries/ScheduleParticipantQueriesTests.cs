using Behind.Core.Database;
using Behind.Core.Database.Auditing;
using Behind.Core.Database.Queries;

namespace Behind.Tests.CoreDatabase.Queries;

public sealed class ScheduleParticipantQueriesTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static async Task<Guid> User(AbstractCoreContext context, string name, string email, bool confirmed = true)
    {
        var user = new HumanUser(name, [new Email(email, confirmed ? DateTimeOffset.UtcNow : null)], None);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        return context.Entry(user).Property<Guid>("Id").CurrentValue;
    }

    private static async Task<Guid> Role(AbstractCoreContext context, string name, params Guid[] members)
    {
        var role = new UserRole(name);
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        var roleId = context.Entry(role).Property<Guid>("Id").CurrentValue;

        foreach (var member in members)
        {
            await context.Set<Dictionary<string, object>>("UserRoles").AddAsync(new() { ["UserId"] = member, ["RoleId"] = roleId });
        }

        await context.SaveChangesAsync();

        return roleId;
    }

    private static ScheduleParticipants Participants(params (ScheduleParticipantKind Kind, string Value)[] items) =>
        new(items.Select(item => new ScheduleParticipant(item.Kind, item.Value)).ToList());

    [Fact]
    public async Task Emails_CombinesRoleMembersUsersAndCustomAddressesWithoutDuplicates()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var member = await User(context, "Member", "member@example.test");
        var direct = await User(context, "Direct", "direct@example.test");
        var unconfirmed = await User(context, "Pending", "pending@example.test", false);
        var outsider = await User(context, "Outsider", "outsider@example.test");
        var role = await Role(context, "Auditors", member, unconfirmed);

        var emails = await new ScheduleParticipantQueries(context).Emails(Participants(
            (ScheduleParticipantKind.Role, role.ToString()),
            (ScheduleParticipantKind.User, direct.ToString()),
            (ScheduleParticipantKind.User, member.ToString()),
            (ScheduleParticipantKind.Email, "MEMBER@example.test"),
            (ScheduleParticipantKind.Email, "external@example.test")));

        Assert.Equal(["direct@example.test", "external@example.test", "member@example.test"], emails.Order(StringComparer.OrdinalIgnoreCase).Select(email => email.ToLowerInvariant()));
        Assert.DoesNotContain("outsider@example.test", emails);
        Assert.NotEqual(Guid.Empty, outsider);
    }

    [Fact]
    public async Task Emails_IgnoresUnknownAndSystemIdentities()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);

        var emails = await new ScheduleParticipantQueries(context).Emails(Participants(
            (ScheduleParticipantKind.Role, Guid.NewGuid().ToString()),
            (ScheduleParticipantKind.User, DomainConstants.SystemUserId.ToString()),
            (ScheduleParticipantKind.User, Guid.NewGuid().ToString())));

        Assert.Empty(emails);
    }

    [Fact]
    public async Task Directory_SearchesRolesAndLimitsUsers()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        await User(context, "Alice Auditor", "alice@example.test");
        await User(context, "Albert Auditor", "albert@example.test");
        await User(context, "Bob", "bob@example.test");
        await Role(context, "Audit Team");

        var queries = new ScheduleParticipantQueries(context);
        var directory = await queries.Directory("audit", 1);
        var everyone = await queries.Directory(null, 100);

        Assert.Equal(["Audit Team"], directory.Roles.Select(role => role.Name));
        Assert.Single(directory.Users);
        Assert.StartsWith("Albert Auditor", directory.Users[0].Label);
        Assert.Contains(everyone.Roles, role => role.Name == "Audit Team");
        Assert.DoesNotContain(everyone.Users, user => user.Id == DomainConstants.SystemUserId || user.Id == DomainConstants.GuestUserId);
    }

    [Fact]
    public async Task Label_NamesRolesAndUsersAndKeepsEmailsAndUnknownValues()
    {
        await using var context = _factory.CreateAuditedContext(AuditActor.Anonymous);
        var user = await User(context, "Alice", "alice@example.test");
        var role = await Role(context, "Auditors");
        var missing = Guid.NewGuid().ToString();

        var labels = await new ScheduleParticipantQueries(context).Label(Participants(
            (ScheduleParticipantKind.Role, role.ToString()),
            (ScheduleParticipantKind.User, user.ToString()),
            (ScheduleParticipantKind.Email, "ops@example.test"),
            (ScheduleParticipantKind.Role, missing)));

        Assert.Equal("Auditors", labels[0].Label);
        Assert.StartsWith("Alice", labels[1].Label);
        Assert.Equal("ops@example.test", labels[2].Label);
        Assert.Equal(missing, labels[3].Label);
    }
}
