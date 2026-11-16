using Behind.Common;
using static Behind.Common.FunctionalExtensions;
using TotpSecret = Behind.Core.Common.Configuration.Totp;

namespace Behind.Core.WebApi.TwoFactor;

public static class TwoFactorCodeExtensions
{
    extension(string value)
    {
        public string WithoutSpaces() => new(value.Where(character => !char.IsWhiteSpace(character)).ToArray());

        public Option<TotpSecret> ToTotp() => TotpSecret.TryCreate(value, out var secret) ? Some(secret) : None;
    }
}
