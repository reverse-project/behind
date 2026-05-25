using System.ComponentModel.DataAnnotations;
using Behind.Core.Common;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignUpEndpoints
{
    public sealed record PasswordRequest([Required, DataType(DataType.EmailAddress)] string Email, [Required] string Password);

    public static async Task<IResult> SignUpPassword(UserManager<ApplicationUser> userManager, PasswordRequest request)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Results.Conflict("Email already in use");
        }

        var user = new ApplicationUser
        {
            UserName = request.Email, NormalizedUserName = request.Email.ToUpperInvariant(),
            Password = PasswordHash.Create(request.Password)
        };
        
        var result = await userManager.CreateAsync(user, request.Password);
        
        return result.Succeeded
            ? Results.StatusCode(204)
            : Results.BadRequest(result.Errors.Select(e => e.Description));
    }
}