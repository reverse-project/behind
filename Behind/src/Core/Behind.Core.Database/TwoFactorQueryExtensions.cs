using Behind.Core.Database.Aggregates;
using Behind.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Behind.Core.Database;

internal static class TwoFactorQueryExtensions
{
    extension(AbstractCoreContext context)
    {
        public IQueryable<UserTotp> TotpsOf(Guid userId) =>
            context.UserTotps.Where(totp => EF.Property<Guid>(totp, UserTotpConfiguration.UserIdProperty) == userId);

        public IQueryable<UserPasskey> PasskeysOf(Guid userId) =>
            context.UserPasskeys.Where(passkey =>
                EF.Property<Guid>(passkey, UserPasskeyConfiguration.UserIdProperty) == userId);

        public IQueryable<UserRecoveryCode> RecoveryCodesOf(Guid userId) =>
            context.UserRecoveryCodes.Where(code =>
                EF.Property<Guid>(code, UserRecoveryCodeConfiguration.UserIdProperty) == userId);
    }
}
