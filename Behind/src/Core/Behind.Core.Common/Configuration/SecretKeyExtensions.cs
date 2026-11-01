using Behind.Common;
using OtpNet;
using TotpValue = Behind.Core.Common.Configuration.Totp;

namespace Behind.Core.Common.Configuration;

public static class SecretKeyExtensions
{
    extension(Option<SecretKey> secretKey)
    {
        public bool VerifyTotp(string code, DateTimeOffset now) =>
            secretKey.Match(
                none: () => false,
                key => key is TotpValue totp && totp.Verify(code, now));
    }

    extension(TotpValue totp)
    {
        public string ToConfigurationValue() => $"totp://{totp.Value}";

        public string ToOtpUri(string account, string issuer) =>
            $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}"
            + $"?secret={totp.Value.TrimEnd('=')}"
            + $"&issuer={Uri.EscapeDataString(issuer)}"
            + $"&algorithm={TotpValue.HashMode.ToString().ToLowerInvariant()}"
            + "&digits=6&period=30";

        private bool Verify(string code, DateTimeOffset now) =>
            new OtpNet.Totp(Base32Encoding.ToBytes(totp.Value), mode: TotpValue.HashMode)
                .VerifyTotp(now.UtcDateTime, code, out _, new VerificationWindow());
    }

    extension(SecurityConfiguration configuration)
    {
        public bool VerifyTotp(string code, DateTimeOffset now) =>
            configuration.SecretKey.VerifyTotp(code, now);
    }
}
