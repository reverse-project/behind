using Behind.Common.Logging;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Behind.Common.WebApi.Handlers;

public sealed class BadRequestExceptionHandler(ILogger<BadRequestExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, 
        Exception exception, 
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequestException)
        {
            logger.LogDebug(
                "Exception of type {ExceptionType} is not a bad request, passing it to the next handler",
                () => [exception.GetType().Name]);

            return false;
        }

        logger.LogWarning(
            badRequestException,
            "Rejected {Method} {Path} as a bad request",
            () => [context.Request.Method, context.Request.Path.Value]);
        
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
            
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid Request Parameters",
            Detail = badRequestException.Message
        }, cancellationToken);

        return true;
    }
}