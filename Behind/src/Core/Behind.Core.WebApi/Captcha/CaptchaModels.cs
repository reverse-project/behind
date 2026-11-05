using System.ComponentModel.DataAnnotations;

namespace Behind.Core.WebApi.Captcha;

public sealed record CaptchaChallenge(string Id, string Image);

public sealed record CaptchaResponse([property: Required] string Id, [property: Required] string Answer);

public sealed record CaptchaTicket(DateTimeOffset Expires, string Answer);

public interface ICaptchaRequest
{
    CaptchaResponse? Captcha { get; }
}

public interface ICaptchaService
{
    CaptchaChallenge Create();

    bool Verify(CaptchaResponse? response);
}
