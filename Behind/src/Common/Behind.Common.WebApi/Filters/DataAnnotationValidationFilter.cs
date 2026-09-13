using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Behind.Common.WebApi.Filters;
public sealed class DataAnnotationValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argToValidate = context.Arguments.OfType<T>().FirstOrDefault();

        if (argToValidate is null)
        {
            return Results.BadRequest();
        }

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(argToValidate);

        var isValid = Validator.TryValidateObject(argToValidate, validationContext, validationResults, true);

        if (isValid)
        {
            return await next(context);
        }
        
        var errors = validationResults
            .Where(r => r.MemberNames.Any())
            .SelectMany(r => r.MemberNames.Select(m => new { Member = m, Error = r.ErrorMessage }))
            .GroupBy(x => x.Member)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.Error ?? "Invalid").ToArray()
            );

        return Results.ValidationProblem(errors);
    }
}