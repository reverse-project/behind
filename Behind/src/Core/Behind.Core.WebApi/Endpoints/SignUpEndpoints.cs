using System.ComponentModel.DataAnnotations;
using Behind.Common.WebApi.Middleware;
using Behind.Core.Domain;
using Behind.Core.WebApi.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Behind.Core.WebApi.Endpoints;

public sealed class SignUpEndpoints
{
    public sealed record PasswordRequest([property: Required, DataType(DataType.EmailAddress)] string Email,
        [property: Required] string Password,
        [property: MaxLength(DisplayNameExtensions.MaxLength)] string? DisplayName = null);

    [IgnoreETag]
    public static async Task<IResult> SignUpPassword(UserManager<ApplicationUser> userManager, PasswordRequest request)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return Results.Conflict("Email already in use");
        }

        var user = new ApplicationUser
        {
            DisplayName = request.DisplayName.ToDisplayName(),
            Emails = [new Email(request.Email)]
        };
        
        var result = await userManager.CreateAsync(user, request.Password);
        
        return result.Succeeded
            ? Results.StatusCode(204)
            : Results.BadRequest(result.Errors.Select(e => e.Description));
    }
}