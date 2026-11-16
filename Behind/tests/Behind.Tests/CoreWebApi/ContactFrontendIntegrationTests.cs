using Behind.Contact.WebApi;
using Behind.Core.WebApi;
using System.Text.RegularExpressions;

namespace Behind.Tests.CoreWebApi;

public class ContactFrontendIntegrationTests
{
    [Theory]
    [InlineData("controllers/contact.js")]
    [InlineData("controllers/contactmanager.js")]
    [InlineData("models/contact-widget.js")]
    [InlineData("viewmodels/contact.js")]
    [InlineData("viewmodels/contactmanager.js")]
    [InlineData("views/contact.html")]
    [InlineData("views/contactwidget.html")]
    [InlineData("views/contactmanager.html")]
    [InlineData("locales/en-US.json")]
    [InlineData("locales/hu-HU.json")]
    public async Task InstalledModule_ServesEveryPublicAndManagementSurface(string resource)
    {
        var catalog = ModuleCatalog.Create([ContactModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);
        await using var host = await StaticFrontendHost.StartAsync(
            EmbeddedFrontend.CreateFileProvider(null, catalog));

        using var response = await host.Client.GetAsync("/modules/contact/" + resource);

        Assert.True(response.IsSuccessStatusCode);
        Assert.NotEmpty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ManagementRecipient_IsOptionalInServedForm()
    {
        var catalog = ModuleCatalog.Create([ContactModule.Descriptor]).GetOrElse(ModuleCatalog.Empty);
        await using var host = await StaticFrontendHost.StartAsync(
            EmbeddedFrontend.CreateFileProvider(null, catalog));

        var html = await host.Client.GetStringAsync("/modules/contact/views/contactmanager.html");
        var input = Regex.Match(html, "<input\\b[^>]*\\bid=(?:\"contact-form-recipient\"|'contact-form-recipient'|contact-form-recipient)(?:\\s|/|>)[^>]*>", RegexOptions.IgnoreCase);

        Assert.True(input.Success);
        Assert.DoesNotMatch("\\brequired\\b", input.Value);
        Assert.Contains("contact.manager.form.recipientHint", html);
    }

    [Fact]
    public void AbsentModule_DoesNotPublishContactWidgetsOrFrontend()
    {
        var provider = EmbeddedFrontend.CreateFileProvider(null, ModuleCatalog.Empty);

        Assert.False(provider.GetFileInfo("modules/contact/models/contact-widget.js").Exists);
        Assert.Empty(ModuleCatalog.Empty.Widgets);
    }
}
