using System.Text;
using System.Text.Json;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class OidcFrontendTests
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
    public void Auth_LoadsProvidersAndRedeemsTicketsThroughTheSharedSignInFlow()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("http.get(\"/sign/in/oidc/providers\")", auth);
        Assert.Contains("Array.isArray(response.data)", auth);
        Assert.Contains("http.postWithCredentials(\"/sign/in/oidc/redeem\", { ticket: ticket })", auth);
        Assert.Contains("return response.ok ? completed(response)", auth);
        Assert.Contains("403: bannedMessage", auth);
        Assert.Contains("http.backendUrl(\"/sign/in/oidc/\" + encodeURIComponent(id))", auth);
        Assert.Contains("new URL(path, window.location.origin)", Read("models/http.js"));
        Assert.Contains("xhrFields: withCredentials ? { withCredentials: true } : undefined", Read("models/http.js"));
    }

    [Fact]
    public void SignIn_RedeemsAndScrubsCallbackTicketsAndStartsProviderNavigation()
    {
        var viewModel = Read("viewmodels/signin.js");

        Assert.Contains("if (params.oidc)", viewModel);
        Assert.Contains("if (params.oidcError)", viewModel);
        Assert.Contains("context.scrub(\"#/sign-in\")", viewModel);
        Assert.Contains("delete params.oidc", viewModel);
        Assert.Contains("auth.redeemOidc(ticket)", viewModel);
        Assert.Contains("self.message(result.message)", viewModel);
        Assert.Contains("var url = auth.oidcUrl(provider.id)", viewModel);
        Assert.Contains("window.location.assign(url)", viewModel);
        Assert.Contains("\"#/signin\": \"controllers/signin\"", Read("app.js"));
    }

    [Fact]
    public void SignIn_ShowsAccessibleProviderButtonsOutsideTheEmailForm()
    {
        var view = Read("views/signin.html");
        var formEnd = view.IndexOf("</form>", StringComparison.Ordinal);
        var providers = view.IndexOf("foreach: oidcProviders", StringComparison.Ordinal);

        Assert.True(formEnd >= 0);
        Assert.True(providers > formEnd);
        Assert.Contains("'aria-label': $parent.oidcSignInLabel(displayName)", view);
        Assert.Contains("visible: !twoFactor.active() && oidcProviders().length > 0", view);
        Assert.Contains("\"signin.oidc.provider\"", Read("viewmodels/signin.js"));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locale_ProvidesOidcAccessibleLabelsAndGenericErrors(string locale)
    {
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));
        var messages = document.RootElement.GetProperty("messages");

        Assert.True(messages.TryGetProperty("signin.oidc.provider", out _));
        Assert.True(messages.TryGetProperty("signin.oidc.error", out _));
    }
}
