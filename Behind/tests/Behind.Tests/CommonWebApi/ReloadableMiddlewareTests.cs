using Behind.Common.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Behind.Common.Configuration;
using Behind.Tests.Helpers;

namespace Behind.Tests.CommonWebApi;

public class ReloadableMiddlewareTests
{
    private sealed record Greeting(string Text);

    [Fact]
    public async Task UseReloadable_RebuildsTheBranchWhenTheOptionsChange()
    {
        var configuration = new ReloadableMemoryConfiguration(new Dictionary<string, string?> { ["Greeting:Text"] = "hello" });
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<IConfiguration>(configuration);
        builder.Services.AddReloadableOptions(c => new Greeting(c["Greeting:Text"]!));
        await using var app = builder.Build();
        var builds = 0;
        app.UseReloadable<Greeting>((branch, greeting) =>
        {
            builds++;
            branch.Use((context, next) =>
            {
                context.Response.Headers["X-Greeting"] = greeting.Text;

                return next(context);
            });
        });
        app.MapGet("/", () => Results.Ok());
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

        var first = (await client.GetAsync("/")).Headers.GetValues("X-Greeting").Single();
        var again = (await client.GetAsync("/")).Headers.GetValues("X-Greeting").Single();
        configuration.Update("Greeting:Text", "bye");
        var changed = (await client.GetAsync("/")).Headers.GetValues("X-Greeting").Single();

        Assert.Equal(["hello", "hello", "bye"], [first, again, changed]);
        Assert.Equal(2, builds);
        await app.StopAsync();
    }
}
