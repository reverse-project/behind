using Behind.Core.Database;

namespace Behind.Core.WebApi.TwoFactor;

public static class TwoFactorStatusExtensions
{
    extension(ITwoFactorQueries.TwoFactorStatus status)
    {
        public IReadOnlyList<TwoFactorMethod> Methods =>
            new[]
                {
                    (Offered: status.TotpEnabled, Method: TwoFactorMethod.Totp),
                    (Offered: status.Passkeys.Count > 0, Method: TwoFactorMethod.Passkey),
                    (Offered: status.IsEnabled && status.RecoveryCodesRemaining > 0, Method: TwoFactorMethod.Recovery)
                }
                .Where(entry => entry.Offered)
                .Select(entry => entry.Method)
                .ToList();
    }
}
