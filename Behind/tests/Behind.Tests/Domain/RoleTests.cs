namespace Behind.Tests.Domain;

public class RoleTests
{
    // ── UserRole ──────────────────────────────────────────────────────────

    [Fact]
    public void UserRole_SingleArgCtor_SetsName()
    {
        var role = new UserRole("Editor");
        Assert.Equal("Editor", role.Name);
    }

    [Fact]
    public void UserRole_SingleArgCtor_SetsGrantsToNone()
    {
        var role = new UserRole("Editor");
        Option<IEnumerable<Grant>> none = None;
        Assert.Equal(none, role.Grants);
    }

    [Fact]
    public void UserRole_TwoArgCtor_SetsName()
    {
        var role = new UserRole("Admin", None);
        Assert.Equal("Admin", role.Name);
    }

    [Fact]
    public void UserRole_TwoArgCtor_WithGrants_GrantsIsSome()
    {
        var grant = new Grant("users:read");
        var role = new UserRole("Admin", Some<IEnumerable<Grant>>([grant]));
        bool isSome = role.Grants.Match(() => false, _ => true);
        Assert.True(isSome);
    }

    [Fact]
    public void UserRole_TwoArgCtor_WithGrants_ContainsGrant()
    {
        var grant = new Grant("users:write");
        var role = new UserRole("Admin", Some<IEnumerable<Grant>>([grant]));
        var grants = role.Grants.Match(() => Enumerable.Empty<Grant>(), g => g);
        Assert.Contains(grant, grants);
    }

    [Fact]
    public void UserRole_TwoArgCtor_WithNone_GrantsIsNone()
    {
        var role = new UserRole("Viewer", None);
        Option<IEnumerable<Grant>> none = None;
        Assert.Equal(none, role.Grants);
    }

    // ── SystemRole ────────────────────────────────────────────────────────

    [Fact]
    public void SystemRole_NameEqualsSystemConstant()
    {
        var role = new SystemRole(None);
        Assert.Equal(DomainConstants.System, role.Name);
    }

    [Fact]
    public void SystemRole_NameIsLiterallySystem()
    {
        var role = new SystemRole(None);
        Assert.Equal("System", role.Name);
    }

    [Fact]
    public void SystemRole_WithNoneGrants_GrantsIsNone()
    {
        var role = new SystemRole(None);
        Option<IEnumerable<Grant>> none = None;
        Assert.Equal(none, role.Grants);
    }

    [Fact]
    public void SystemRole_WithGrants_GrantsIsSome()
    {
        var grant = new Grant("system:all");
        var role = new SystemRole(Some<IEnumerable<Grant>>([grant]));
        bool isSome = role.Grants.Match(() => false, _ => true);
        Assert.True(isSome);
    }
}
