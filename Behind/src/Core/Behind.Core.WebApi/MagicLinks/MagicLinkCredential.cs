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
    private const string NonceFormat = "N";
    private const string KeyPurpose = "magic-link";

    public string Token => $"{Payload(Nonce, Random)}{Separator}{Hash}";

    public static MagicLinkCredential Create(TokenNonce nonce, Uuid secret, string pepper)
    {
        var random = RandomNumberGenerator.CreateUrlSafeToken();

        return new MagicLinkCredential(nonce, random, Payload(nonce, random).SignHmacSha256(KeyOf(secret, pepper)));
    }

    public static Option<MagicLinkCredential> TryParse(string? token) =>
        token?.Split(Separator) is [var nonce, var random, var hash]
        && Guid.TryParseExact(nonce, NonceFormat, out var parsedNonce)
            ? Some(new MagicLinkCredential(parsedNonce, random, hash))
            : None;

    public bool IsSignedBy(IEnumerable<Uuid> secrets, string pepper) =>
        secrets.Aggregate(
            false,
            (signed, secret) => Payload(Nonce, Random).VerifyHmacSha256(Hash, KeyOf(secret, pepper)) | signed);

    private static byte[] KeyOf(Uuid secret, string pepper) => secret.Value.DeriveKey(pepper, KeyPurpose);

    private static string Payload(TokenNonce nonce, string random) =>
        $"{nonce.Nonce.ToString(NonceFormat)}{Separator}{random}";
}
