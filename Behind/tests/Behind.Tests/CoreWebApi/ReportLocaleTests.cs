using Behind.Core.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Behind.Tests.CoreWebApi;

public sealed class ReportLocaleTests
{
    [Fact]
    public async Task ReportSubmissionNeverBypassesPublicEvidenceLocaleIsolation()
    {
        var endpoint = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/reports"), 0);
        endpoint.Metadata.Add(new AuthorizeAttribute { Policy = "Report.Write" });
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.SetEndpoint(endpoint.Build());
        var authorization = new FakeAuthorizationService("Report.Write", "Report.Manage");
        Assert.False(await ContentLocaleMiddleware.IsManagement(http, authorization));
    }
}
