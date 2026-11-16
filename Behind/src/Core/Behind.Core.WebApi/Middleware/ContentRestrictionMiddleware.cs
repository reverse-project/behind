using Behind.Common;
using Behind.Common.Database;
using Behind.Core.Database;
using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Behind.Core.WebApi.Middleware;

public sealed class ContentRestrictionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        AbstractCoreContext context,
        ISessionQueries sessions,
        TimeProvider timeProvider,
        IOptionsMonitor<ContentRestrictionsConfiguration> configuration)
    {
        context.HiddenContentIds = [];
        if (!configuration.CurrentValue.Enabled)
        {
            await next(httpContext);
            return;
        }

        var session = await httpContext.FindCurrentSession(sessions);
        var birthday = await session.Match(
            () => Task.FromResult<DateOnly?>(null),
            current => context.Users.Where(user => EF.Property<Guid>(user, "Id") == current.UserId)
                .Select(user => user.Birthday).FirstOrDefaultAsync(httpContext.RequestAborted));
        var age = ContentAgePolicy.Age(birthday, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime));
        var currentRestrictions = context.ContentRestrictions.LatestOf(context.ContentRestrictions);
        var hidden = await currentRestrictions
            .Where(item => item.MinimumAge > 0 && item.MinimumAge > age)
            .Select(item => item.EntityId).Distinct().ToListAsync(httpContext.RequestAborted);
        context.HiddenContentIds = await context.InheritContentRestrictions(hidden, httpContext.RequestAborted);
        httpContext.Response.Headers.CacheControl = "private, no-store";
        httpContext.Response.OnStarting(() =>
        {
            httpContext.Response.Headers.CacheControl = "private, no-store";
            httpContext.Response.Headers.Remove("ETag");
            httpContext.Response.Headers.Remove("Last-Modified");
            return Task.CompletedTask;
        });

        await next(httpContext);
    }
}
