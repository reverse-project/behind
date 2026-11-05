using Behind.Common;
using OtpNet;
using static Behind.Common.FunctionalExtensions;
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

        public string ToOtpUri(string account, string issuer, OtpHashMode mode = TotpValue.HashMode) =>
            $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}"
            + $"?secret={totp.Value.TrimEnd('=')}"
            + $"&issuer={Uri.EscapeDataString(issuer)}"
            + $"&algorithm={mode.ToString().ToLowerInvariant()}"
            + "&digits=6&period=30";

        public Option<long> VerifiedStep(
            string code,
            DateTimeOffset now,
            OtpHashMode mode = TotpValue.HashMode,
            int tolerance = 0) =>
            new OtpNet.Totp(Base32Encoding.ToBytes(totp.Value), mode: mode)
                .VerifyTotp(now.UtcDateTime, code, out var step, new VerificationWindow(tolerance, tolerance))
                ? Some(step)
                : None;

        private bool Verify(string code, DateTimeOffset now) => totp.VerifiedStep(code, now) != None;
    }

    extension(SecurityConfiguration configuration)
    {
        public bool VerifyTotp(string code, DateTimeOffset now) =>
            configuration.SecretKey.VerifyTotp(code, now);
    }
}
