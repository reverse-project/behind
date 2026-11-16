using System.Text;
using System.Text.Json;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class MessagesFrontendTests
{
    private static readonly IFileProvider Provider = EmbeddedFrontend.CreateFileProvider();

    private static string Read(string path)
    {
        var file = Provider.GetFileInfo(path);
        Assert.True(file.Exists, path);

        using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Theory]
    [InlineData("controllers/messages.js")]
    [InlineData("viewmodels/messages.js")]
    [InlineData("views/messages.html")]
    [InlineData("models/messages.js")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void ManagerController_BindsItsViewAndViewModel()
    {
        var controller = Read("controllers/messages.js");

        Assert.Contains("text!views/messages.html", controller);
        Assert.Contains("viewmodels/messages", controller);
        Assert.Contains("createController(view,", controller);
    }

    [Fact]
    public void Routes_ServeTheManagerBeforeTheAliasCatchAll()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/manage/messages\": \"controllers/messages\"", app);
        Assert.True(
            app.IndexOf("\"#/manage/messages\"", StringComparison.Ordinal)
            < app.IndexOf("var aliasRoute = \"#/:slug\"", StringComparison.Ordinal));
    }

    [Fact]
    public void MessagesModel_CallsTheMessageEndpoints()
    {
        var model = Read("models/messages.js");

        Assert.Contains("managing(\"/messages/manage\", query)", model);
        Assert.Contains("http.get(\"/messages/\" + id)", model);
        Assert.Contains("http.post(\"/messages\", draft(fields))", model);
        Assert.Contains("http.post(\"/messages/\" + id, draft(fields))", model);
        Assert.Contains("http.post(\"/messages/\" + id + \"/delete\")", model);
        Assert.Contains("http.post(\"/messages/\" + id + \"/restore\")", model);
        Assert.Contains("http.post(\"/messages/\" + id + \"/hard/delete\")", model);
    }

    [Fact]
    public void ViewModel_OffersSoftDeleteRestoreAndPermanentDeleteByGrant()
    {
        var viewModel = Read("viewmodels/messages.js");
        var view = Read("views/messages.html");

        Assert.Contains("session.hasGrant(\"Message.Read\")", viewModel);
        Assert.Contains("session.hasGrant(\"Message.Write\")", viewModel);
        Assert.Contains("session.hasGrant(\"Message.Delete\")", viewModel);
        Assert.Contains("click: remove", view);
        Assert.Contains("click: restore", view);
        Assert.Contains("click: hardDelete", view);
        Assert.Contains("showDeleted", view);
        Assert.Contains("letterPager", view);
    }

    [Fact]
    public void Editor_OffersLevelsRichContentAndLocalSchedule()
    {
        var viewModel = Read("viewmodels/messages.js");
        var view = Read("views/messages.html");

        Assert.Contains("danger: \"messages.level.danger\"", viewModel);
        Assert.Contains("richEditor: { value: content", view);
        Assert.Contains("type=\"datetime-local\"", view);
        Assert.Contains("toISOString()", viewModel);
        Assert.Contains("startsAt: toIso(self.startsAt())", viewModel);
        Assert.Contains("endsAt: toIso(self.endsAt())", viewModel);
        Assert.Contains("messages.get(row.id)", viewModel);
    }

    [Fact]
    public void Administration_OffersTheMessagesToMessageReaders()
    {
        var administration = Read("viewmodels/administration.js");

        Assert.Contains("href: \"#/manage/messages\", allowed: canReadMessages", administration);
        Assert.Contains("session.hasGrant(\"Message.Read\")", administration);
    }

    [Fact]
    public void AuditLog_NamesTheMessageEntity()
    {
        Assert.Contains("Message: \"audit.entities.Message\"", Read("viewmodels/auditlog.js"));
    }

    [Fact]
    public void WidgetCatalog_ContainsTheMessageWidget()
    {
        Assert.Contains("{ key: \"core.message\", name: \"widgets.message\" }", Read("models/widgets.js"));
    }

    [Fact]
    public void LayoutEditor_OffersAMessageSelectAndSavesTheMessage()
    {
        var view = Read("views/layouts.html");
        var viewModel = Read("viewmodels/layouts.js");

        Assert.Contains("type() === 'core.message'", view);
        Assert.Contains("options: $root.messages, optionsText: 'name', optionsValue: 'id'", view);
        Assert.Contains("value: message", view);
        Assert.Contains("message: ko.observable(data.message || \"\")", viewModel);
        Assert.Contains("message: widget.message()", viewModel);
        Assert.Contains("pageSize: 50", viewModel);
        Assert.Contains("data.page < data.pageCount", viewModel);
        Assert.Contains("loadMessages(data.page + 1)", viewModel);
        Assert.Contains("valueAllowUnset: true", view);
        Assert.Contains("visible: messageLoadError", view);
    }

    [Fact]
    public void Editor_AllowsOptionalTitlesAndTheWidgetSurfacesFailures()
    {
        Assert.DoesNotContain("self.title().trim() !== \"\"", Read("viewmodels/messages.js"));
        var template = Read("components/sitewidget.html");
        Assert.Contains("visible: error, text: error", template[..template.IndexOf("<!-- ko ifnot: type === 'core.message'", StringComparison.Ordinal)]);
        Assert.Contains("dismissedMessageKey === dismissalKey(message)", Read("components/sitewidget.js"));
    }

    [Fact]
    public void SiteWidget_RendersASafeDismissibleAlertAndDisposesItsTimer()
    {
        var script = Read("components/sitewidget.js");
        var template = Read("components/sitewidget.html");

        Assert.Contains("\"/widgets/messages/\" + encodeURIComponent(messageId)", script);
        Assert.Contains("response.status === 404", script);
        Assert.Contains("\"behind.message.dismissed.\" + message.id + \".\" + message.version", script);
        Assert.Contains("alert alert-\" + level + \" alert-dismissible behind-message", script);
        Assert.Contains("}, 60000)", script);
        Assert.Contains("window.clearInterval(timer)", script);
        Assert.Contains("type === 'core.message'", template);
        Assert.Contains("html: message().content", template);
        Assert.Contains("click: dismissMessage", template);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locales_TranslateAllMessageTexts(string locale)
    {
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));
        var messages = document.RootElement.GetProperty("messages");
        var view = Read("views/messages.html") + Read("viewmodels/messages.js") + Read("models/messages.js");
        var keys = System.Text.RegularExpressions.Regex.Matches(view, "[\"'](messages\\.[A-Za-z0-9.]+)[\"']")
            .Select(match => match.Groups[1].Value).Distinct().ToList();
        keys.AddRange(["audit.entities.Message", "administration.pages.messages", "layouts.message", "layouts.messageCaption", "widgets.message", "widgets.messageDismiss"]);

        foreach (var key in keys)
        {
            Assert.True(messages.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()), key);
        }
    }
}
