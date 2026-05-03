using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Behind.Common.WebApi.Handlers;

public sealed class BadRequestExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, 
        Exception exception, 
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequestException)
        {
            return false;
        }
        
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