using Behind.Core.WebApi.Captcha;
using Behind.Core.WebApi.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Behind.Tests.CoreWebApi.Captcha;

public class CaptchaFilterTests
{
    private static readonly CaptchaResponse Response = new("id", "ABCDE");

    private readonly ICaptchaService _service = Substitute.For<ICaptchaService>();

    private async Task<object?> Invoke(object? request)
    {
        var filter = new CaptchaFilter<SignInEndpoints.PasswordRequest>(
            _service,
            NullLogger<CaptchaFilter<SignInEndpoints.PasswordRequest>>.Instance);

        var context = new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext(),
            request is null ? [] : [request]);

        return await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>("next"));
    }

    [Fact]
    public async Task Invoke_ContinuesWhenTheCaptchaIsAccepted()
    {
        _service.Verify(Response).Returns(true);

        Assert.Equal("next", await Invoke(new SignInEndpoints.PasswordRequest("a@b.c", "pw", Response)));
    }

    [Fact]
    public async Task Invoke_RejectsAWrongCaptchaWithABadRequest()
    {
        _service.Verify(Response).Returns(false);

        var result = await Invoke(new SignInEndpoints.PasswordRequest("a@b.c", "pw", Response));

        var bad = Assert.IsType<BadRequest<CaptchaRejectedResponse>>(result);
        Assert.Equal(CaptchaRejectedResponse.InvalidCode, bad.Value!.Code);
    }

    [Fact]
    public async Task Invoke_RejectsARequestWithoutACaptcha()
    {
        _service.Verify(null).Returns(false);

        Assert.IsType<BadRequest<CaptchaRejectedResponse>>(
            await Invoke(new SignInEndpoints.PasswordRequest("a@b.c", "pw")));
    }

    [Fact]
    public async Task Invoke_RejectsAMissingRequestBody()
    {
        Assert.IsType<BadRequest<CaptchaRejectedResponse>>(await Invoke(null));
        _service.DidNotReceiveWithAnyArgs().Verify(default);
    }
}
