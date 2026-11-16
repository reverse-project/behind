using Behind.Common;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;

namespace Behind.Core.WebApi.TwoFactor;

public static class TwoFactorUserExtensions
{
    extension(ApplicationUser user)
    {
        public string Account => user.Emails.FirstOrDefault()?.Value ?? user.Id.ToString();

        public string FriendlyName => string.IsNullOrWhiteSpace(user.DisplayName) ? user.Account : user.DisplayName;
    }

    extension(string? name)
    {
        public string ToPasskeyName() =>
            string.IsNullOrWhiteSpace(name)
                ? TwoFactorEnrollment.DefaultPasskeyName
                : name.Trim().Shortened(UserPasskey.MaxNameLength);
    }
}
