using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Common.WebApi;

public static class ReloadableMiddlewareExtensions
{
    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseReloadable<T>(Action<IApplicationBuilder, T> configure) where T : class
        {
            var options = app.ApplicationServices.GetRequiredService<IOptions<T>>();

            return app.Use(next =>
            {
                Branch<T>? branch = null;

                return context =>
                {
                    var current = options.Value;
                    var active = Volatile.Read(ref branch);

                    if (active is null || !ReferenceEquals(active.Options, current))
                    {
                        active = new Branch<T>(current, new Lazy<RequestDelegate>(() => Build(app, configure, current, next)));
                        Volatile.Write(ref branch, active);
                    }

                    return active.Pipeline.Value(context);
                };
            });
        }
    }

    private static RequestDelegate Build<T>(
        IApplicationBuilder app,
        Action<IApplicationBuilder, T> configure,
        T options,
        RequestDelegate next)
    {
        var branch = app.New();
        configure(branch, options);
        branch.Run(next);

        return branch.Build();
    }

    private sealed record Branch<T>(T Options, Lazy<RequestDelegate> Pipeline);
}
