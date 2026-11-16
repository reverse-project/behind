using Behind.Core.WebApi.TwoFactor;
using Fido2NetLib;

namespace Behind.Tests.CoreWebApi.TwoFactor;

public sealed class PasskeyCeremoniesTests : IDisposable
{
    private static readonly Uri Origin = new("https://example.test/app/");
    private const string OriginText = "https://example.test";

    private readonly PasskeyCeremonies _ceremonies = new(NullLogger<PasskeyCeremonies>.Instance);
    private readonly VirtualAuthenticator _authenticator = new();
    private readonly Guid _userId = Guid.NewGuid();

    public void Dispose() => _authenticator.Dispose();

    private Fido2User User() => new() { Id = _userId.ToByteArray(), Name = "ada@test.com", DisplayName = "Ada" };

    private static Task<bool> Unique(byte[] _) => Task.FromResult(true);

    private async Task<RegisteredPublicKeyCredentialResult> Register()
    {
        var options = _ceremonies.BeginRegistration(Origin, User(), []);
        var credential = await _ceremonies.CompleteRegistration(
            Origin,
            options,
            _authenticator.Register(options, OriginText),
            Unique);

        return new RegisteredPublicKeyCredentialResult(options, credential);
    }

    private sealed record RegisteredPublicKeyCredentialResult(
        CredentialCreateOptions Options,
        Option<Fido2NetLib.Objects.RegisteredPublicKeyCredential> Credential);

    [Fact]
    public void BeginRegistration_AsksForANonResidentCredentialOfTheRelyingParty()
    {
        var options = _ceremonies.BeginRegistration(Origin, User(), []);

        Assert.Equal("example.test", options.Rp.Id);
        Assert.Equal(PasskeyCeremonies.RelyingPartyName, options.Rp.Name);
        Assert.Equal(_userId.ToByteArray(), options.User.Id);
        Assert.NotEmpty(options.Challenge);
    }

    [Fact]
    public async Task CompleteRegistration_AcceptsTheCredentialOfTheAuthenticator()
    {
        var result = await Register();

        var credential = result.Credential.Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal(_authenticator.CredentialId, credential.Id);
        Assert.NotEmpty(credential.PublicKey);
    }

    [Fact]
    public async Task CompleteRegistration_RejectsAnotherOrigin()
    {
        var options = _ceremonies.BeginRegistration(Origin, User(), []);

        var credential = await _ceremonies.CompleteRegistration(
            Origin,
            options,
            _authenticator.Register(options, "https://evil.test"),
            Unique);

        Assert.Equal(Behind.Common.FunctionalExtensions.None, credential);
    }

    [Fact]
    public async Task CompleteRegistration_RejectsACredentialThatIsAlreadyRegistered()
    {
        var options = _ceremonies.BeginRegistration(Origin, User(), []);

        var credential = await _ceremonies.CompleteRegistration(
            Origin,
            options,
            _authenticator.Register(options, OriginText),
            _ => Task.FromResult(false));

        Assert.Equal(Behind.Common.FunctionalExtensions.None, credential);
    }

    [Fact]
    public async Task CompleteRegistration_RejectsAnotherChallenge()
    {
        var options = _ceremonies.BeginRegistration(Origin, User(), []);
        var other = _ceremonies.BeginRegistration(Origin, User(), []);

        var credential = await _ceremonies.CompleteRegistration(
            Origin,
            options,
            _authenticator.Register(other, OriginText),
            Unique);

        Assert.Equal(Behind.Common.FunctionalExtensions.None, credential);
    }

    [Fact]
    public async Task CompleteAssertion_AcceptsTheSignatureOfTheRegisteredKey()
    {
        var registered = await Register();
        var credential = registered.Credential.Match(() => throw new InvalidOperationException(), found => found);
        var options = _ceremonies.BeginAssertion(Origin, [new(credential.Id)]);

        var result = await _ceremonies.CompleteAssertion(
            Origin,
            options,
            _authenticator.Assert(options, OriginText),
            credential.PublicKey,
            credential.SignCount,
            _userId.ToByteArray());

        var verified = result.Match(() => throw new InvalidOperationException(), found => found);

        Assert.Equal(_authenticator.CredentialId, verified.CredentialId);
        Assert.Equal(1u, verified.SignCount);
    }

    [Fact]
    public async Task CompleteAssertion_RejectsAReplayedCounter()
    {
        var registered = await Register();
        var credential = registered.Credential.Match(() => throw new InvalidOperationException(), found => found);
        var options = _ceremonies.BeginAssertion(Origin, [new(credential.Id)]);

        var result = await _ceremonies.CompleteAssertion(
            Origin,
            options,
            _authenticator.Assert(options, OriginText),
            credential.PublicKey,
            5,
            _userId.ToByteArray());

        Assert.Equal(Behind.Common.FunctionalExtensions.None, result);
    }

    [Fact]
    public async Task CompleteAssertion_RejectsAnotherKey()
    {
        var registered = await Register();
        var credential = registered.Credential.Match(() => throw new InvalidOperationException(), found => found);
        using var intruder = new VirtualAuthenticator();
        var options = _ceremonies.BeginAssertion(Origin, [new(credential.Id)]);

        var response = intruder.Assert(options, OriginText, claimedCredentialId: credential.Id);

        var result = await _ceremonies.CompleteAssertion(
            Origin,
            options,
            response,
            credential.PublicKey,
            credential.SignCount,
            _userId.ToByteArray());

        Assert.Equal(Behind.Common.FunctionalExtensions.None, result);
    }

    [Fact]
    public async Task CompleteAssertion_RejectsTheUserHandleOfAnotherUser()
    {
        var registered = await Register();
        var credential = registered.Credential.Match(() => throw new InvalidOperationException(), found => found);
        var options = _ceremonies.BeginAssertion(Origin, [new(credential.Id)]);

        var result = await _ceremonies.CompleteAssertion(
            Origin,
            options,
            _authenticator.Assert(options, OriginText, Guid.NewGuid().ToByteArray()),
            credential.PublicKey,
            credential.SignCount,
            _userId.ToByteArray());

        Assert.Equal(Behind.Common.FunctionalExtensions.None, result);
    }
}
