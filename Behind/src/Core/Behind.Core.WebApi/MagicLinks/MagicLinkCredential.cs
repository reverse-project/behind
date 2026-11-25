using System.Security.Cryptography;
using Behind.Common;
using Behind.Common.Security;
using Behind.Core.Common.Configuration;
using Behind.Core.Domain;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.MagicLinks;

public sealed record MagicLinkCredential(TokenNonce Nonce, string Random, string Hash)
{
    private const char Separator = '.';
    private const char AddressSeparator = '~';
    private const string SignInAddressPurpose = "magic-link-sign-in-address";
    private const string NonceFormat = "N";
    public const string MagicLinkPurpose = "magic-link";
    public const string PasswordResetPurpose = "password-reset";

    public string Token => $"{Payload(Nonce, Random)}{Separator}{Hash}";

    public static MagicLinkCredential Create(
        TokenNonce nonce,
        Uuid secret,
        string pepper,
        string purpose = MagicLinkPurpose,
        string? signInAddress = null)
    {
        var random = RandomNumberGenerator.CreateUrlSafeToken();
        if (signInAddress is not null)
        {
            var binding = AddressPayload(nonce, random, signInAddress)
                .SignHmacSha256(KeyOf(secret, pepper, SignInAddressPurpose));
            random = $"{random}{AddressSeparator}{binding}";
        }

        return new MagicLinkCredential(
            nonce,
            random,
            Payload(nonce, random).SignHmacSha256(KeyOf(secret, pepper, purpose)));
    }

    public static Option<MagicLinkCredential> TryParse(string? token) =>
        token?.Split(Separator) is [var nonce, var random, var hash]
        && Guid.TryParseExact(nonce, NonceFormat, out var parsedNonce)
            ? Some(new MagicLinkCredential(parsedNonce, random, hash))
            : None;

    public bool IsSignedBy(IEnumerable<Uuid> secrets, string pepper, string purpose = MagicLinkPurpose) =>
        secrets.Aggregate(
            false,
            (signed, secret) => Payload(Nonce, Random).VerifyHmacSha256(Hash, KeyOf(secret, pepper, purpose)) | signed);

    internal string? FindSignInAddress(IEnumerable<Email> emails, IEnumerable<Uuid> secrets, string pepper)
    {
        if (Random.Split(AddressSeparator) is not [var random, var binding])
        {
            return null;
        }

        return emails.FirstOrDefault(email => email.ConfirmedAt is not null
            && secrets.Any(secret => AddressPayload(Nonce, random, email.Value)
                .VerifyHmacSha256(binding, KeyOf(secret, pepper, SignInAddressPurpose))))?.Value;
    }

    private static string AddressPayload(TokenNonce nonce, string random, string address) =>
        $"{Payload(nonce, random)}{Separator}{address}";

    private static byte[] KeyOf(Uuid secret, string pepper, string purpose) =>
        secret.Value.DeriveKey(pepper, purpose);

    private static string Payload(TokenNonce nonce, string random) =>
        $"{nonce.Nonce.ToString(NonceFormat)}{Separator}{random}";
}
