using Behind.Common;
using Behind.Common.Logging;
using Microsoft.Extensions.Logging;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.TwoFactor;

public interface IPasskeyCeremonies
{
    CredentialCreateOptions BeginRegistration(
        Uri origin,
        Fido2User user,
        IReadOnlyList<PublicKeyCredentialDescriptor> existing);

    Task<Option<RegisteredPublicKeyCredential>> CompleteRegistration(
        Uri origin,
        CredentialCreateOptions options,
        AuthenticatorAttestationRawResponse response,
        Func<byte[], Task<bool>> isUnique,
        CancellationToken cancellationToken = default);

    AssertionOptions BeginAssertion(Uri origin, IReadOnlyList<PublicKeyCredentialDescriptor> allowed);

    Task<Option<VerifyAssertionResult>> CompleteAssertion(
        Uri origin,
        AssertionOptions options,
        AuthenticatorAssertionRawResponse response,
        byte[] publicKey,
        uint signCount,
        byte[] userHandle,
        CancellationToken cancellationToken = default);
}

public sealed class PasskeyCeremonies(ILogger<PasskeyCeremonies> logger) : IPasskeyCeremonies
{
    public const string RelyingPartyName = "Behind";

    public CredentialCreateOptions BeginRegistration(
        Uri origin,
        Fido2User user,
        IReadOnlyList<PublicKeyCredentialDescriptor> existing) =>
        Relying(origin).RequestNewCredential(new RequestNewCredentialParams
        {
            User = user,
            ExcludeCredentials = existing,
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Discouraged,
                UserVerification = UserVerificationRequirement.Preferred
            },
            AttestationPreference = AttestationConveyancePreference.None
        });

    public async Task<Option<RegisteredPublicKeyCredential>> CompleteRegistration(
        Uri origin,
        CredentialCreateOptions options,
        AuthenticatorAttestationRawResponse response,
        Func<byte[], Task<bool>> isUnique,
        CancellationToken cancellationToken = default) =>
        await Verified(
            async () => await Relying(origin).MakeNewCredentialAsync(
                new MakeNewCredentialParams
                {
                    AttestationResponse = response,
                    OriginalOptions = options,
                    IsCredentialIdUniqueToUserCallback = (parameters, _) => isUnique(parameters.CredentialId)
                },
                cancellationToken),
            "registration");

    public AssertionOptions BeginAssertion(Uri origin, IReadOnlyList<PublicKeyCredentialDescriptor> allowed) =>
        Relying(origin).GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowed,
            UserVerification = UserVerificationRequirement.Preferred
        });

    public async Task<Option<VerifyAssertionResult>> CompleteAssertion(
        Uri origin,
        AssertionOptions options,
        AuthenticatorAssertionRawResponse response,
        byte[] publicKey,
        uint signCount,
        byte[] userHandle,
        CancellationToken cancellationToken = default) =>
        await Verified(
            async () => await Relying(origin).MakeAssertionAsync(
                new MakeAssertionParams
                {
                    AssertionResponse = response,
                    OriginalOptions = options,
                    StoredPublicKey = publicKey,
                    StoredSignatureCounter = signCount,
                    IsUserHandleOwnerOfCredentialIdCallback = (parameters, _) =>
                        Task.FromResult(parameters.UserHandle.AsSpan().SequenceEqual(userHandle))
                },
                cancellationToken),
            "assertion");

    private static Fido2 Relying(Uri origin) =>
        new(
            new Fido2Configuration
            {
                ServerDomain = origin.Host,
                ServerName = RelyingPartyName,
                Origins = new HashSet<string> { origin.GetLeftPart(UriPartial.Authority) }
            },
            null!);

    private async Task<Option<T>> Verified<T>(Func<Task<T>> verify, string ceremony)
    {
        try
        {
            return Some(await verify());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Passkey {Ceremony} rejected: {Reason}",
                () => [ceremony, exception.Message]);

            return None;
        }
    }
}
