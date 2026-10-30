namespace Behind.Tests.Domain;

public class AccessTokenTests
{
    private static readonly User TestUser = new GuestUser(None);

    // ── Create() ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_NonceIsNotEmpty()
    {
        var token = AccessToken.Create(TestUser);
        Assert.NotEqual(Guid.Empty, token.Nonce.Nonce);
    }

    [Fact]
    public void Create_IssuedAtIsCloseToUtcNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var token = AccessToken.Create(TestUser);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.InRange(token.IssuedAt, before, after);
    }

    [Fact]
    public void Create_ExpiresAtEqualsIssuedAtPlusDefaultDuration()
    {
        var token = AccessToken.Create(TestUser);
        Assert.Equal(token.IssuedAt + DomainConstants.AccessTokenDuration, token.ExpiresAt);
    }

    [Fact]
    public void Create_UserIsPreserved()
    {
        var token = AccessToken.Create(TestUser);
        Assert.Equal(TestUser, token.User);
    }

    // ── Create(user, duration) ────────────────────────────────────────────

    [Fact]
    public void Create_WithDuration_ExpiresAtEqualsIssuedAtPlusDuration()
    {
        var duration = TimeSpan.FromHours(2);
        var token = AccessToken.Create(TestUser, duration);
        Assert.Equal(token.IssuedAt + duration, token.ExpiresAt);
    }

    [Fact]
    public void Create_WithDuration_NonceIsNotEmpty()
    {
        var token = AccessToken.Create(TestUser, TimeSpan.FromMinutes(5));
        Assert.NotEqual(Guid.Empty, token.Nonce.Nonce);
    }

    // ── Create(user, issuedAt, duration) ──────────────────────────────────

    [Fact]
    public void Create_WithIssuedAt_UsesProvidedIssuedAt()
    {
        var issuedAt = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var token = AccessToken.Create(TestUser, issuedAt, TimeSpan.FromMinutes(10));
        Assert.Equal(issuedAt, token.IssuedAt);
    }

    [Fact]
    public void Create_WithIssuedAt_CalculatesExpiresAt()
    {
        var issuedAt = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var duration = TimeSpan.FromMinutes(15);
        var token = AccessToken.Create(TestUser, issuedAt, duration);
        Assert.Equal(issuedAt + duration, token.ExpiresAt);
    }

    // ── CreateExisting ────────────────────────────────────────────────────

    [Fact]
    public void CreateExisting_PreservesNonce()
    {
        var nonce = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow;
        var token = AccessToken.CreateExisting(nonce, TestUser, issuedAt,
                           DomainConstants.AccessTokenDuration);
        Assert.Equal(nonce, token.Nonce.Nonce);
    }

    [Fact]
    public void CreateExisting_CalculatesExpiresAt()
    {
        var nonce = Guid.NewGuid();
        var issuedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var duration = TimeSpan.FromMinutes(30);
        var token = AccessToken.CreateExisting(nonce, TestUser, issuedAt, duration);
        Assert.Equal(issuedAt + duration, token.ExpiresAt);
    }

    [Fact]
    public void CreateExisting_PreservesUser()
    {
        var token = AccessToken.CreateExisting(Guid.NewGuid(), TestUser,
                        DateTimeOffset.UtcNow, DomainConstants.AccessTokenDuration);
        Assert.Equal(TestUser, token.User);
    }

    // ── Uniqueness ────────────────────────────────────────────────────────

    [Fact]
    public void TwoSuccessiveCreates_HaveDifferentNonces()
    {
        var t1 = AccessToken.Create(TestUser);
        var t2 = AccessToken.Create(TestUser);
        Assert.NotEqual(t1.Nonce, t2.Nonce);
    }
}

public class RefreshTokenTests
{
    private static readonly User TestUser = new GuestUser(None);

    // ── Create() ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_NonceIsNotEmpty()
    {
        var token = RefreshToken.Create(TestUser);
        Assert.NotEqual(Guid.Empty, token.Nonce.Nonce);
    }

    [Fact]
    public void Create_IssuedAtIsCloseToUtcNow()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var token = RefreshToken.Create(TestUser);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.InRange(token.IssuedAt, before, after);
    }

    [Fact]
    public void Create_ExpiresAtEqualsIssuedAtPlusDefaultDuration()
    {
        var token = RefreshToken.Create(TestUser);
        Assert.Equal(token.IssuedAt + DomainConstants.RefreshTokenDuration, token.ExpiresAt);
    }

    [Fact]
    public void Create_UserIsPreserved()
    {
        var token = RefreshToken.Create(TestUser);
        Assert.Equal(TestUser, token.User);
    }

    // ── Create(user, duration) ────────────────────────────────────────────

    [Fact]
    public void Create_WithDuration_ExpiresAtEqualsIssuedAtPlusDuration()
    {
        var duration = TimeSpan.FromDays(7);
        var token = RefreshToken.Create(TestUser, duration);
        Assert.Equal(token.IssuedAt + duration, token.ExpiresAt);
    }

    // ── Create(user, issuedAt, duration) ──────────────────────────────────

    [Fact]
    public void Create_WithIssuedAt_UsesProvidedIssuedAt()
    {
        var issuedAt = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var token = RefreshToken.Create(TestUser, issuedAt, TimeSpan.FromDays(14));
        Assert.Equal(issuedAt, token.IssuedAt);
    }

    [Fact]
    public void Create_WithIssuedAt_CalculatesExpiresAt()
    {
        var issuedAt = new DateTimeOffset(2024, 6, 1, 12, 0, 0, TimeSpan.Zero);
        var duration = TimeSpan.FromDays(14);
        var token = RefreshToken.Create(TestUser, issuedAt, duration);
        Assert.Equal(issuedAt + duration, token.ExpiresAt);
    }

    // ── CreateExisting ────────────────────────────────────────────────────

    [Fact]
    public void CreateExisting_PreservesNonce()
    {
        var nonce = Guid.NewGuid();
        var issuedAt = DateTimeOffset.UtcNow;
        var token = RefreshToken.CreateExisting(nonce, TestUser, issuedAt,
                           DomainConstants.RefreshTokenDuration);
        Assert.Equal(nonce, token.Nonce.Nonce);
    }

    [Fact]
    public void CreateExisting_CalculatesExpiresAt()
    {
        var nonce = Guid.NewGuid();
        var issuedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var duration = TimeSpan.FromDays(30);
        var token = RefreshToken.CreateExisting(nonce, TestUser, issuedAt, duration);
        Assert.Equal(issuedAt + duration, token.ExpiresAt);
    }

    // ── Uniqueness ────────────────────────────────────────────────────────

    [Fact]
    public void TwoSuccessiveCreates_HaveDifferentNonces()
    {
        var t1 = RefreshToken.Create(TestUser);
        var t2 = RefreshToken.Create(TestUser);
        Assert.NotEqual(t1.Nonce, t2.Nonce);
    }
}
