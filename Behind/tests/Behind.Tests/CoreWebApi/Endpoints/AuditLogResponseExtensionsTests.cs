using Behind.Core.WebApi.Endpoints;

namespace Behind.Tests.CoreWebApi.Endpoints;

public class AuditLogResponseExtensionsTests
{
    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    [Fact]
    public void ToActor_MapsEveryKindOfUser()
    {
        var unknownId = Guid.NewGuid();

        Assert.Equal(
            new AuditLogActor("HumanUser", "Ada", ["a@example.com", "b@example.com"]),
            new HumanUser("Ada", [new Email("a@example.com"), new Email("b@example.com")], None).ToActor(),
            ActorComparer.Instance);
        Assert.Equal("System", new SystemUser(None).ToActor().Label);
        Assert.Equal("Guest", new GuestUser(None).ToActor().Label);
        Assert.Equal($"Unknown user {unknownId}", new UnknownUser(unknownId).ToActor().Label);
        Assert.Equal("BannedUser", new BannedUser().ToActor().Kind);
    }

    [Fact]
    public void ToChanges_UpdatedListsEveryDifferenceAsText()
    {
        var changes = new AuditLogEntry.Updated(
        [
            new PropertyDifference("Count", 1, 2),
            new PropertyDifference("Name", null, "x")
        ]).ToChanges();

        Assert.Equal([new AuditLogChange("Count", "1", "2"), new AuditLogChange("Name", null, "x")], changes);
    }

    [Fact]
    public void ToChanges_OwnershipAndMoveListTheIdentifiers()
    {
        Assert.Equal(
            [new AuditLogChange("Owner", First.ToString(), Second.ToString())],
            new AuditLogEntry.ChangeOwnership(First, Second).ToChanges());
        Assert.Equal(
            [new AuditLogChange("Parent", First.ToString(), Second.ToString())],
            new AuditLogEntry.Moved(First, Second).ToChanges());
    }

    [Fact]
    public void ToChanges_ActionsWithoutPayloadHaveNoChanges()
    {
        Assert.Empty(new AuditLogEntry.Created().ToChanges());
        Assert.Empty(new AuditLogEntry.Deleted().ToChanges());
        Assert.Empty(new AuditLogEntry.HardDeleted().ToChanges());
        Assert.Empty(new AuditLogEntry.Restored().ToChanges());
    }

    [Fact]
    public void ToItem_CarriesTheEntryAndItsBehalfOfActor()
    {
        var timestamp = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var item = new AuditLogEntry(
            "User",
            "7",
            new AuditLogEntry.Restored(),
            new SystemUser(None),
            timestamp,
            Some<User>(new GuestUser(None))).ToItem();

        Assert.Equal(timestamp, item.Timestamp);
        Assert.Equal("User", item.EntityName);
        Assert.Equal("7", item.EntityKey);
        Assert.Equal("Restored", item.Action);
        Assert.Equal("Guest", item.OnBehalfOf!.Label);
    }

    [Theory]
    [InlineData("ImpersonationStarted")]
    [InlineData("ImpersonationEnded")]
    public void ToItem_ImpersonationEntries_ShowTheImpersonatorOnBehalfOfTheTarget(string action)
    {
        AuditLogEntry.AuditAction auditAction = action == "ImpersonationStarted"
            ? new AuditLogEntry.ImpersonationStarted()
            : new AuditLogEntry.ImpersonationEnded();

        var item = new AuditLogEntry(
            nameof(ValidSession),
            "9",
            auditAction,
            new HumanUser("Admin", [new Email("admin@example.com")], None),
            DateTimeOffset.UtcNow,
            Some<User>(new HumanUser("Target", [new Email("target@example.com")], None))).ToItem();

        Assert.Equal(action, item.Action);
        Assert.Empty(item.Changes);
        Assert.Equal("Admin", item.Actor.Label);
        Assert.Equal("Target", item.OnBehalfOf!.Label);
    }

    private sealed class ActorComparer : IEqualityComparer<AuditLogActor>
    {
        public static readonly ActorComparer Instance = new();

        public bool Equals(AuditLogActor? x, AuditLogActor? y) =>
            x is not null
            && y is not null
            && x.Kind == y.Kind
            && x.Label == y.Label
            && x.Emails.SequenceEqual(y.Emails);

        public int GetHashCode(AuditLogActor obj) => obj.Label.GetHashCode();
    }
}
