namespace Behind.Tests.Domain;

public class WallOfShameEntryTests
{
    private static readonly Reason TestReason = new Reason("Repeated spam");

    // ── Reason-only constructor ────────────────────────────────────────────

    [Fact]
    public void ReasonOnlyCtor_SetsReason()
    {
        var entry = new WallOfShameEntry(TestReason);
        Assert.Equal(TestReason, entry.Reason);
    }

    [Fact]
    public void ReasonOnlyCtor_SetsUserToGuestUser()
    {
        var entry = new WallOfShameEntry(TestReason);
        Assert.IsType<GuestUser>(entry.User);
    }

    [Fact]
    public void ReasonOnlyCtor_SetsGivenByToGuestUser()
    {
        var entry = new WallOfShameEntry(TestReason);
        Assert.IsType<GuestUser>(entry.GivenBy);
    }

    [Fact]
    public void ReasonOnlyCtor_SetsExpiresToNone()
    {
        var entry = new WallOfShameEntry(TestReason);
        Option<DateTimeOffset> none = None;
        Assert.Equal(none, entry.Expires);
    }

    // ── Full (primary) constructor ─────────────────────────────────────────

    [Fact]
    public void FullCtor_SetsUser()
    {
        var user = new SystemUser(None);
        var givenBy = new SystemUser(None);
        var expires = DateTimeOffset.UtcNow.AddDays(30);

        var entry = new WallOfShameEntry(user, Some(expires), TestReason, givenBy);
        Assert.Equal(user, entry.User);
    }

    [Fact]
    public void FullCtor_SetsGivenBy()
    {
        var user = new SystemUser(None);
        var givenBy = new SystemUser(None);
        var expires = DateTimeOffset.UtcNow.AddDays(30);

        var entry = new WallOfShameEntry(user, Some(expires), TestReason, givenBy);
        Assert.Equal(givenBy, entry.GivenBy);
    }

    [Fact]
    public void FullCtor_SetsReason()
    {
        var user = new SystemUser(None);
        var givenBy = new SystemUser(None);
        var entry = new WallOfShameEntry(user, None, TestReason, givenBy);
        Assert.Equal(TestReason, entry.Reason);
    }

    [Fact]
    public void FullCtor_SetsExpiresToSome_WhenProvided()
    {
        var user = new SystemUser(None);
        var givenBy = new SystemUser(None);
        var expires = new DateTimeOffset(2025, 12, 31, 0, 0, 0, TimeSpan.Zero);

        var entry = new WallOfShameEntry(user, Some(expires), TestReason, givenBy);

        var value = entry.Expires.Match(() => DateTimeOffset.MinValue, e => e);
        Assert.Equal(expires, value);
    }

    [Fact]
    public void FullCtor_SetsExpiresToNone_WhenNonePassed()
    {
        var user = new SystemUser(None);
        var givenBy = new SystemUser(None);

        var entry = new WallOfShameEntry(user, None, TestReason, givenBy);

        Option<DateTimeOffset> none = None;
        Assert.Equal(none, entry.Expires);
    }

    // ── Extension methods ──────────────────────────────────────────────────

    [Fact]
    public void WithUser_ChangesUser()
    {
        var entry = new WallOfShameEntry(TestReason);
        var newUser = new SystemUser(None);
        var updated = entry.WithUser(newUser);
        Assert.Equal(newUser, updated.User);
    }

    [Fact]
    public void WithUser_DoesNotMutateOriginal()
    {
        var entry = new WallOfShameEntry(TestReason);
        var newUser = new SystemUser(None);
        _ = entry.WithUser(newUser);
        Assert.IsType<GuestUser>(entry.User);
    }

    [Fact]
    public void WithExpires_ChangesExpires()
    {
        var entry = new WallOfShameEntry(TestReason);
        var expires = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var updated = entry.WithExpires(Some(expires));

        var value = updated.Expires.Match(() => DateTimeOffset.MinValue, e => e);
        Assert.Equal(expires, value);
    }

    [Fact]
    public void WithExpires_CanSetToNone()
    {
        var user = new SystemUser(None);
        var givenBy = new SystemUser(None);
        var expires = DateTimeOffset.UtcNow.AddDays(7);

        var entry = new WallOfShameEntry(user, Some(expires), TestReason, givenBy);
        var updated = entry.WithExpires(None);

        Option<DateTimeOffset> none = None;
        Assert.Equal(none, updated.Expires);
    }

    [Fact]
    public void WithGivenBy_ChangesGivenBy()
    {
        var entry = new WallOfShameEntry(TestReason);
        var givenBy = new SystemUser(None);
        var updated = entry.WithGivenBy(givenBy);
        Assert.Equal(givenBy, updated.GivenBy);
    }

    [Fact]
    public void WithGivenBy_DoesNotMutateOriginal()
    {
        var entry = new WallOfShameEntry(TestReason);
        var givenBy = new SystemUser(None);
        _ = entry.WithGivenBy(givenBy);
        Assert.IsType<GuestUser>(entry.GivenBy);
    }
}
