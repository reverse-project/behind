namespace Behind.Tests.Domain;

public class UserTests
{
    // ── HumanUser ─────────────────────────────────────────────────────────

    [Fact]
    public void HumanUser_NoArgCtor_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new HumanUser());
    }

    [Fact]
    public void HumanUser_EmptyEmailCollection_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new HumanUser(string.Empty, Array.Empty<Email>(), None));
    }

    [Fact]
    public void HumanUser_WithOneEmail_Succeeds()
    {
        var email = new Email("user@example.com");
        var user = new HumanUser("Test User", [email], None);
        Assert.NotNull(user);
    }

    [Fact]
    public void HumanUser_ContainsSuppliedEmail()
    {
        var email = new Email("user@example.com");
        var user = new HumanUser("Test User", [email], None);
        Assert.Contains(email, user.Emails);
    }

    [Fact]
    public void HumanUser_ContainsMultipleEmails()
    {
        var e1 = new Email("a@example.com");
        var e2 = new Email("b@example.com");
        var user = new HumanUser("Test User", [e1, e2], None);
        Assert.Contains(e1, user.Emails);
        Assert.Contains(e2, user.Emails);
    }

    [Fact]
    public void HumanUser_WithRoles_StoresRoles()
    {
        var email = new Email("user@example.com");
        var role = new UserRole("Admin");
        var user = new HumanUser("Test User", [email], Some<IEnumerable<Role>>([role]));
        bool isSome = user.Roles.Match(() => false, _ => true);
        Assert.True(isSome);
    }

    [Fact]
    public void HumanUser_WithNoneRoles_RolesIsNone()
    {
        var email = new Email("user@example.com");
        var user = new HumanUser("Test User", [email], None);
        Option<IEnumerable<Role>> none = None;
        Assert.Equal(none, user.Roles);
    }

    // ── SystemUser ────────────────────────────────────────────────────────

    [Fact]
    public void SystemUser_CanBeCreated()
    {
        var user = new SystemUser(None);
        Assert.NotNull(user);
    }

    [Fact]
    public void SystemUser_RolesPreserved()
    {
        var role = new UserRole("Ops");
        var user = new SystemUser(Some<IEnumerable<Role>>([role]));
        bool isSome = user.Roles.Match(() => false, _ => true);
        Assert.True(isSome);
    }

    // ── GuestUser ─────────────────────────────────────────────────────────

    [Fact]
    public void GuestUser_CanBeCreated()
    {
        var user = new GuestUser(None);
        Assert.NotNull(user);
    }

    [Fact]
    public void GuestUser_RolesIsNone_WhenNonePassed()
    {
        var user = new GuestUser(None);
        Option<IEnumerable<Role>> none = None;
        Assert.Equal(none, user.Roles);
    }

    // ── BannedUser ────────────────────────────────────────────────────────

    [Fact]
    public void BannedUser_CanBeCreated()
    {
        var user = new BannedUser();
        Assert.NotNull(user);
    }

    [Fact]
    public void BannedUser_RolesIsNone()
    {
        var user = new BannedUser();
        Option<IEnumerable<Role>> none = None;
        Assert.Equal(none, user.Roles);
    }
}
