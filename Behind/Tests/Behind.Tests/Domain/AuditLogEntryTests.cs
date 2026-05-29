namespace Behind.Tests.Domain;

public class AuditLogEntryTests
{
    private static readonly DateTimeOffset TestTimestamp =
        new DateTimeOffset(2024, 3, 15, 10, 0, 0, TimeSpan.Zero);

    // ── 4-arg constructor ──────────────────────────────────────────────────

    [Fact]
    public void FourArgCtor_SetsEntityName()
    {
        var entry = new AuditLogEntry("MyEntity", "Key1", "Create", TestTimestamp);
        Assert.Equal("MyEntity", entry.EntityName);
    }

    [Fact]
    public void FourArgCtor_SetsEntityKey()
    {
        var entry = new AuditLogEntry("MyEntity", "Key1", "Create", TestTimestamp);
        Assert.Equal("Key1", entry.EntityKey);
    }

    [Fact]
    public void FourArgCtor_SetsAction()
    {
        var entry = new AuditLogEntry("MyEntity", "Key1", "Create", TestTimestamp);
        Assert.Equal("Create", entry.Action);
    }

    [Fact]
    public void FourArgCtor_SetsTimestamp()
    {
        var entry = new AuditLogEntry("MyEntity", "Key1", "Create", TestTimestamp);
        Assert.Equal(TestTimestamp, entry.Timestamp);
    }

    [Fact]
    public void FourArgCtor_SetsUserToGuestUser()
    {
        var entry = new AuditLogEntry("Entity", "Key1", "Create", TestTimestamp);
        Assert.IsType<GuestUser>(entry.User);
    }

    [Fact]
    public void FourArgCtor_SetsBehalfOfToNone()
    {
        var entry = new AuditLogEntry("Entity", "Key1", "Create", TestTimestamp);
        Option<User> none = None;
        Assert.Equal(none, entry.BehalfOf);
    }

    // ── Primary constructor ────────────────────────────────────────────────

    [Fact]
    public void PrimaryCtor_SetsAllProperties()
    {
        var user = new SystemUser(None);
        var behalfOf = new GuestUser(None);
        var entry = new AuditLogEntry(
            "Ent", "K", "Update",
            user, TestTimestamp,
            Some<User>(behalfOf));

        Assert.Equal("Ent", entry.EntityName);
        Assert.Equal("K", entry.EntityKey);
        Assert.Equal("Update", entry.Action);
        Assert.Equal(user, entry.User);
        Assert.Equal(TestTimestamp, entry.Timestamp);
    }

    [Fact]
    public void PrimaryCtor_PreservesBehalfOf()
    {
        var behalfOf = new GuestUser(None);
        var entry = new AuditLogEntry(
            "E", "K", "A",
            new SystemUser(None), TestTimestamp,
            Some<User>(behalfOf));

        var result = entry.BehalfOf.Match(() => (User?)null, u => u);
        Assert.Equal(behalfOf, result);
    }

    // ── Copy constructor ───────────────────────────────────────────────────

    [Fact]
    public void CopyCtor_KeepsEntityName()
    {
        var original = new AuditLogEntry("OrigName", "K", "A",
            new SystemUser(None), TestTimestamp, None);
        var copy = new AuditLogEntry(original);
        Assert.Equal("OrigName", copy.EntityName);
    }

    [Fact]
    public void CopyCtor_KeepsEntityKey()
    {
        var original = new AuditLogEntry("E", "OrigKey", "A",
            new SystemUser(None), TestTimestamp, None);
        var copy = new AuditLogEntry(original);
        Assert.Equal("OrigKey", copy.EntityKey);
    }

    [Fact]
    public void CopyCtor_KeepsAction()
    {
        var original = new AuditLogEntry("E", "K", "OrigAction",
            new SystemUser(None), TestTimestamp, None);
        var copy = new AuditLogEntry(original);
        Assert.Equal("OrigAction", copy.Action);
    }

    [Fact]
    public void CopyCtor_KeepsTimestamp()
    {
        var original = new AuditLogEntry("E", "K", "A",
            new SystemUser(None), TestTimestamp, None);
        var copy = new AuditLogEntry(original);
        Assert.Equal(TestTimestamp, copy.Timestamp);
    }

    [Fact]
    public void CopyCtor_ResetsUserToGuestUser()
    {
        var original = new AuditLogEntry("E", "K", "A",
            new SystemUser(None), TestTimestamp, None);
        var copy = new AuditLogEntry(original);
        Assert.IsType<GuestUser>(copy.User);
    }

    [Fact]
    public void CopyCtor_ResetsBehalfOfToNone()
    {
        var user = new SystemUser(None);
        var original = new AuditLogEntry("E", "K", "A",
            user, TestTimestamp, Some<User>(user));
        var copy = new AuditLogEntry(original);
        Option<User> none = None;
        Assert.Equal(none, copy.BehalfOf);
    }

    // ── Extension methods ──────────────────────────────────────────────────

    [Fact]
    public void WithUser_ReturnsEntryWithNewUser()
    {
        var entry = new AuditLogEntry("E", "K", "A", TestTimestamp);
        var newUser = new SystemUser(None);
        var updated = entry.WithUser(newUser);
        Assert.Equal(newUser, updated.User);
    }

    [Fact]
    public void WithUser_DoesNotMutateOriginal()
    {
        var entry = new AuditLogEntry("E", "K", "A", TestTimestamp);
        var newUser = new SystemUser(None);
        _ = entry.WithUser(newUser);
        Assert.IsType<GuestUser>(entry.User);
    }

    [Fact]
    public void WithBehalfOf_SetsBehalfOfToSomeWithProvidedUser()
    {
        var entry = new AuditLogEntry("E", "K", "A", TestTimestamp);
        var behalfOf = new GuestUser(None);
        var updated = entry.WithBehalfOf(behalfOf);

        var result = updated.BehalfOf.Match(() => (User?)null, u => u);
        Assert.Equal(behalfOf, result);
    }

    [Fact]
    public void WithBehalfOf_BehalfOfIsSome()
    {
        var entry = new AuditLogEntry("E", "K", "A", TestTimestamp);
        var behalfOf = new GuestUser(None);
        var updated = entry.WithBehalfOf(behalfOf);

        bool isSome = updated.BehalfOf.Match(() => false, _ => true);
        Assert.True(isSome);
    }
}
