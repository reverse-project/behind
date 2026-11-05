using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Behind.Common;
using static Behind.Common.FunctionalExtensions;

namespace Behind.Core.WebApi.Captcha;

public static class CaptchaTicketExtensions
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const char Separator = ':';

    extension(CaptchaTicket ticket)
    {
        public string Seal(byte[] key)
        {
            var plain = Encoding.UTF8.GetBytes($"{ticket.Expires.ToUnixTimeSeconds()}{Separator}{ticket.Answer}");
            var nonce = RandomNumberGenerator.GetBytes(NonceSize);
            var cipher = new byte[plain.Length];
            var tag = new byte[TagSize];

            using var aes = new AesGcm(key, TagSize);
            aes.Encrypt(nonce, plain, cipher, tag);

            return Base64Url.EncodeToString([.. nonce, .. tag, .. cipher]);
        }

        public bool Accepts(string? answer) =>
            answer is not null
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(answer.Trim().ToUpperInvariant()),
                Encoding.UTF8.GetBytes(ticket.Answer));
    }

    extension(CaptchaTicket)
    {
        public static Option<CaptchaTicket> Open(string token, byte[] key) =>
            Try(() => Decrypt(token, key))
                .Run()
                .Match<Option<CaptchaTicket>>(_ => None, ticket => ticket);
    }

    private static Option<CaptchaTicket> Decrypt(string token, byte[] key)
    {
        var bytes = Base64Url.DecodeFromChars(token);

        if (bytes.Length <= NonceSize + TagSize)
        {
            return None;
        }

        var plain = new byte[bytes.Length - NonceSize - TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(
            bytes.AsSpan(0, NonceSize),
            bytes.AsSpan(NonceSize + TagSize),
            bytes.AsSpan(NonceSize, TagSize),
            plain);

        var text = Encoding.UTF8.GetString(plain);
        var separator = text.IndexOf(Separator);

        return separator > 0 && long.TryParse(text[..separator], out var seconds)
            ? Some(new CaptchaTicket(DateTimeOffset.FromUnixTimeSeconds(seconds), text[(separator + 1)..]))
            : None;
    }
}
