namespace Behind.Tests.Domain;

// ── Email ─────────────────────────────────────────────────────────────────────

public class EmailTests
{
    [Fact]
    public void Email_StoresValue()
    {
        var email = new Email("user@example.com");
        Assert.Equal("user@example.com", email.Value);
    }

    [Fact]
    public void TwoEmailsWithSameValue_AreEqual()
    {
        var a = new Email("x@y.com");
        var b = new Email("x@y.com");
        Assert.Equal(a, b);
    }

    [Fact]
    public void TwoEmailsWithDifferentValues_AreNotEqual()
    {
        Assert.NotEqual(new Email("a@y.com"), new Email("b@y.com"));
    }
}

// ── Grant ─────────────────────────────────────────────────────────────────────

public class GrantTests
{
    [Fact]
    public void Grant_StoresName()
    {
        var grant = new Grant("read:data");
        Assert.Equal("read:data", grant.Name);
    }

    [Fact]
    public void TwoGrantsWithSameName_AreEqual()
    {
        Assert.Equal(new Grant("write"), new Grant("write"));
    }
}

// ── Reason ────────────────────────────────────────────────────────────────────

public class ReasonTests
{
    [Fact]
    public void Reason_StoresValue()
    {
        var reason = new Reason("Spamming");
        Assert.Equal("Spamming", reason.Value);
    }
}

// ── TokenNonce ────────────────────────────────────────────────────────────────

public class TokenNonceTests
{
    [Fact]
    public void ImplicitGuidToTokenNonce_ThenBack_RoundTrips()
    {
        var guid = Guid.NewGuid();
        TokenNonce nonce = guid;
        Guid result = nonce;
        Assert.Equal(guid, result);
    }

    [Fact]
    public void ImplicitTokenNonceToString_EqualsGuidToString()
    {
        var guid = Guid.NewGuid();
        var nonce = new TokenNonce(guid);
        string str = nonce;
        Assert.Equal(guid.ToString(), str);
    }

    [Fact]
    public void ToString_MatchesGuidToString()
    {
        var guid = Guid.NewGuid();
        var nonce = new TokenNonce(guid);
        Assert.Equal(guid.ToString(), nonce.ToString());
    }

    [Fact]
    public void Nonce_Property_ReturnsGuid()
    {
        var guid = Guid.NewGuid();
        var nonce = new TokenNonce(guid);
        Assert.Equal(guid, nonce.Nonce);
    }
}

// ── Session variants ──────────────────────────────────────────────────────────

public class SessionTests
{
    [Fact]
    public void ValidSession_StoresUser()
    {
        var user = new GuestUser(None);
        var access = new TokenNonce(Guid.NewGuid());
        var refresh = new TokenNonce(Guid.NewGuid());

        var session = new ValidSession(user, access, refresh);
        Assert.Equal(user, session.User);
    }

    [Fact]
    public void ValidSession_StoresAccessToken()
    {
        var access = new TokenNonce(Guid.NewGuid());
        var refresh = new TokenNonce(Guid.NewGuid());
        var session = new ValidSession(new GuestUser(None), access, refresh);
        Assert.Equal(access, session.AccessToken);
    }

    [Fact]
    public void ValidSession_StoresRefreshToken()
    {
        var access = new TokenNonce(Guid.NewGuid());
        var refresh = new TokenNonce(Guid.NewGuid());
        var session = new ValidSession(new GuestUser(None), access, refresh);
        Assert.Equal(refresh, session.RefreshToken);
    }

    [Fact]
    public void InvalidSession_CanBeCreated()
    {
        var session = new InvalidSession();
        Assert.NotNull(session);
    }

    [Fact]
    public void ValidSession_IsSession()
    {
        Session session = new ValidSession(
            new GuestUser(None),
            new TokenNonce(Guid.NewGuid()),
            new TokenNonce(Guid.NewGuid()));
        Assert.IsType<ValidSession>(session);
    }

    [Fact]
    public void InvalidSession_IsSession()
    {
        Session session = new InvalidSession();
        Assert.IsType<InvalidSession>(session);
    }
}

// ── DomainConstants ───────────────────────────────────────────────────────────

public class DomainConstantsTests
{
    [Fact]
    public void SystemUserId_IsCorrect()
    {
        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000000"),
                     DomainConstants.SystemUserId);
    }

    [Fact]
    public void GuestUserId_IsCorrect()
    {
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000000"),
                     DomainConstants.GuestUserId);
    }

    [Fact]
    public void AccessTokenDuration_IsThirtyMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(30), DomainConstants.AccessTokenDuration);
    }

    [Fact]
    public void RefreshTokenDuration_IsThirtyDays()
    {
        Assert.Equal(TimeSpan.FromDays(30), DomainConstants.RefreshTokenDuration);
    }

    [Fact]
    public void SystemConstant_IsLiterallySystem()
    {
        Assert.Equal("System", DomainConstants.System);
    }
}

// ── ActiveVersion ─────────────────────────────────────────────────────────────

public class ActiveVersionTests
{
    [Fact]
    public void ActiveVersion_StoresEntityType()
    {
        var av = new ActiveVersion("MyEntity", "key-1", 3);
        Assert.Equal("MyEntity", av.EntityType);
    }

    [Fact]
    public void ActiveVersion_StoresEntityKey()
    {
        var av = new ActiveVersion("MyEntity", "key-1", 3);
        Assert.Equal("key-1", av.EntityKey);
    }

    [Fact]
    public void ActiveVersion_StoresVersion()
    {
        var av = new ActiveVersion("MyEntity", "key-1", 3);
        Assert.Equal(3L, av.Version);
    }

    [Fact]
    public void ActiveVersion_DefaultVersionIsZero()
    {
        var av = new ActiveVersion("MyEntity", "key-1");
        Assert.Equal(0L, av.Version);
    }
}

// ── VersionedKey<T> ───────────────────────────────────────────────────────────

public class VersionedKeyTests
{
    // Implicit string → VersionedKey<int>

    [Fact]
    public void ImplicitFromString_KeyOnly_HasKeyAndVersionZero()
    {
        VersionedKey<int>? vk = "42";
        Assert.NotNull(vk);
        Assert.Equal(42, vk!.Key);
        Assert.Equal(0L, vk.Version);
    }

    [Fact]
    public void ImplicitFromString_WithVersion_ParsesKeyAndVersion()
    {
        VersionedKey<int>? vk = "42.5";
        Assert.NotNull(vk);
        Assert.Equal(42, vk!.Key);
        Assert.Equal(5L, vk.Version);
    }

    [Fact]
    public void ImplicitFromNullString_ReturnsNull()
    {
        VersionedKey<int>? vk = (string?)null;
        Assert.Null(vk);
    }

    [Fact]
    public void ImplicitFromString_ThreeParts_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            VersionedKey<int>? vk = "1.2.3";
            _ = vk;
        });
    }

    // VersionedKey<int> → string?

    [Fact]
    public void ImplicitToString_ReturnsKeyToString()
    {
        var vk = new VersionedKey<int>(99, 3);
        string? str = vk;
        Assert.Equal("99", str);
    }

    [Fact]
    public void ImplicitToString_NullInput_ReturnsNull()
    {
        VersionedKey<int>? vk = null;
        string? str = vk;
        Assert.Null(str);
    }

    // Parse<T>

    [Fact]
    public void Parse_ValidString_ReturnsCorrectKey()
    {
        var vk = VersionedKey<int>.Parse<int>("7");
        Assert.Equal(7, vk.Key);
    }

    [Fact]
    public void Parse_ValidStringWithVersion_ReturnsCorrectVersion()
    {
        var vk = VersionedKey<int>.Parse<int>("7.3");
        Assert.Equal(3L, vk.Version);
    }

    // TryParse<T>

    [Fact]
    public void TryParse_ValidKeyOnly_ReturnsTrueAndValue()
    {
        bool ok = VersionedKey<int>.TryParse<int>("42", out var vk);
        Assert.True(ok);
        Assert.NotNull(vk);
        Assert.Equal(42, vk!.Key);
        Assert.Equal(0L, vk.Version);
    }

    [Fact]
    public void TryParse_ValidKeyAndVersion_ReturnsTrueAndValue()
    {
        bool ok = VersionedKey<int>.TryParse<int>("42.7", out var vk);
        Assert.True(ok);
        Assert.NotNull(vk);
        Assert.Equal(42, vk!.Key);
        Assert.Equal(7L, vk.Version);
    }

    // NOTE: TryParse calls Try(...)() (direct invoke) rather than Try(...).Run(),
    // so exceptions thrown by the implicit operator propagate to the caller.

    [Fact]
    public void TryParse_TooManyParts_ThrowsArgumentException()
    {
        // ArgumentException from the implicit operator propagates out of TryParse.
        Assert.Throws<ArgumentException>(
            () => VersionedKey<int>.TryParse<int>("1.2.3", out _));
    }

    [Fact]
    public void TryParse_InvalidJson_ThrowsException()
    {
        // JsonSerializer throws for non-numeric strings; TryParse does not catch it.
        Assert.ThrowsAny<Exception>(
            () => VersionedKey<int>.TryParse<int>("notanumber", out _));
    }

    // ToString()

    [Fact]
    public void ToString_ReturnsKeyColonVersion()
    {
        var vk = new VersionedKey<int>(42, 3);
        Assert.Equal("42:3", vk.ToString());
    }

    [Fact]
    public void ToString_VersionZero_ShowsZero()
    {
        var vk = new VersionedKey<int>(1);
        Assert.Equal("1:0", vk.ToString());
    }
}
