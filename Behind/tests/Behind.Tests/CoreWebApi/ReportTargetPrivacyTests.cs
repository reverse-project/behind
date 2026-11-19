using Behind.Common.WebApi;
using Behind.Core.Domain;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;

namespace Behind.Tests.CoreWebApi;

public sealed class ReportTargetPrivacyTests
{
    [Fact]
    public async Task SourceReportsReserveCapacityBeforeResolvingPrivateEvidence()
    {
        var identity = Substitute.For<IModuleIdentity>();
        identity.UserId(Arg.Any<HttpContext>()).Returns(Guid.NewGuid());
        var source = Substitute.For<IReportEvidenceSource>();
        source.SourceType.Returns("test");
        var targets = Enumerable.Range(0, UserReport.MaxTargets).Select(_ => Guid.NewGuid()).ToArray();
        var result = await ReportEndpoints.Create(new(targets, "reason", "test", Guid.NewGuid()),
            new DefaultHttpContext(), identity, [source], null!, null!, null!, null!, null!, TimeProvider.System, null!);
        Assert.Equal(400, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        await source.DidNotReceive().Find(Arg.Any<HttpContext>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
