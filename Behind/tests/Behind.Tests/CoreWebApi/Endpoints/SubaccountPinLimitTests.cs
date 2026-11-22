using Behind.Core.Common.Configuration;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Behind.Tests.CoreWebApi.Endpoints;

public sealed class SubaccountPinLimitTests
{
    [Theory]
    [InlineData("4", 4)]
    [InlineData(null, 128)]
    [InlineData("0", 128)]
    [InlineData("129", 128)]
    public async Task AllPinRequestsUseConfiguredLimit(string? configuredLimit, int expectedLimit)
    {
        using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(configuredLimit is null
            ? "{}"
            : $$"""{ "Security": { "Accounts": { "SwitchPinMaxLength": {{configuredLimit}} } } }"""));
        var configuration = new ConfigurationBuilder().AddJsonStream(input).Build();
        var settings = new SecurityConfiguration
        {
            Accounts = configuration.GetSection("Security:Accounts").Get<SecurityAccountSettings>() ?? new()
        };
        using var services = new ServiceCollection().AddSingleton(Options.Create(settings)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var filter = new SubaccountFeatureFilter(false);

        foreach (var (pin, expectedStatus) in new (string? Pin, int Status)[]
        {
            (null, 204), ("", 204), (new string('0', expectedLimit), 204), (new string('0', expectedLimit + 1), 400)
        })
        {
            SubaccountEndpoints.IPinRequest[] requests =
            [
                new SubaccountEndpoints.CreateRequest("Child", null, null, true, pin),
                new SubaccountEndpoints.CreateRequest("Child", null, null, true, null, pin),
                new SubaccountEndpoints.UpdateRequest("Child", null, true, pin, false),
                new SubaccountEndpoints.SwitchRequest(pin)
            ];
            foreach (var request in requests)
            {
                var context = new DefaultEndpointFilterInvocationContext(http, [request]);
                var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.NoContent()));
                Assert.Equal(expectedStatus, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            }
        }
    }
}
