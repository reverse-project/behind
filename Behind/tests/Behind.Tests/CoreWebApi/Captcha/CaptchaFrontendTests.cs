using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi.Captcha;

public class CaptchaFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Model_LoadsTheChallengeFromTheServer()
    {
        Assert.Contains("http.get(\"/captcha\")", Read("models/captcha.js"));
    }

    [Theory]
    [InlineData("views/signin.html")]
    [InlineData("views/signup.html")]
    public void Pages_ShowTheCaptcha(string view)
    {
        Assert.Contains("<captcha params=\"captcha: captcha\"></captcha>", Read(view));
    }

    [Fact]
    public void Component_IsRegisteredAndShowsTheImageAndTheAnswerField()
    {
        var template = Read("components/captcha.html");

        Assert.Contains("ko.components.register(\"captcha\"", Read("app.js"));
        Assert.Contains("attr: { src: image }", template);
        Assert.Contains("value: answer", template);
        Assert.Contains("click: refresh", template);
    }

    [Theory]
    [InlineData("viewmodels/signin.js")]
    [InlineData("viewmodels/signup.js")]
    public void Pages_UseTheCaptchaForm(string viewModel)
    {
        Assert.Contains("viewmodels/captchaform", Read(viewModel));
    }

    [Fact]
    public void CaptchaForm_SendsTheAnswerAndRequestsANewPictureAfterEveryAttempt()
    {
        var form = Read("viewmodels/captchaform.js");

        Assert.Contains("action(self.captcha.response())", form);
        Assert.Contains(".finally(self.captcha.refresh)", form);
        Assert.Contains("self.captcha.refresh()", form);
    }

    [Fact]
    public void Auth_SendsTheCaptchaWithEveryProtectedRequest()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("password: password, captcha: captcha", auth);
        Assert.Contains("displayName: displayName, captcha: captcha", auth);
        Assert.Contains("\"/sign/up/confirm/resend\", { email: email, captcha: captcha }", auth);
        Assert.Contains("function: magicLinkFunction || \"SignIn\", captcha: captcha", auth);
    }

    [Fact]
    public void Auth_ExplainsARejectedCaptcha()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("response.data.code === \"captchaInvalid\"", auth);
        Assert.Contains("i18n.t(\"auth.captcha.invalid\")", auth);
    }
}
