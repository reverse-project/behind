using Behind.Core.Common;
using Microsoft.AspNetCore.Identity;

namespace Behind.Core.WebApi.Identity;

public class DomainPasswordHasher<TUser> : IPasswordHasher<TUser> where TUser : class
{
    public string HashPassword(TUser user, string password)
    {
        return PasswordHash.Create(password);
    }

    public PasswordVerificationResult VerifyHashedPassword(TUser user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
        {
            return PasswordVerificationResult.Failed;
        }

        var hashRecord = PasswordHash.CreateFromExisting(hashedPassword);

        return hashRecord.Verify(providedPassword)
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
    }
}