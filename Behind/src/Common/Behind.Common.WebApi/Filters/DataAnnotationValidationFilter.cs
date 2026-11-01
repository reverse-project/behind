using System.ComponentModel.DataAnnotations;
using Behind.Common.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Behind.Common.WebApi.Filters;
public sealed class DataAnnotationValidationFilter<T>(ILogger<DataAnnotationValidationFilter<T>> logger)
    : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argToValidate = context.Arguments.OfType<T>().FirstOrDefault();

        if (argToValidate is null)
        {
            logger.LogWarning(
                "Rejected {Method} {Path}: request body of type {RequestType} is missing",
                () => [context.HttpContext.Request.Method, context.HttpContext.Request.Path.Value, typeof(T).Name]);

            return Results.BadRequest();
        }

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(argToValidate);

        var isValid = Validator.TryValidateObject(argToValidate, validationContext, validationResults, true);

        if (isValid)
        {
            logger.LogDebug("Validated request of type {RequestType}", () => [typeof(T).Name]);

            return await next(context);
        }

        logger.LogWarning(
            "Rejected {Method} {Path}: request of type {RequestType} failed validation for {InvalidMembers}",
            () =>
            [
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path.Value,
                typeof(T).Name,
                validationResults.SelectMany(r => r.MemberNames).Distinct().ToArray()
            ]);
        
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