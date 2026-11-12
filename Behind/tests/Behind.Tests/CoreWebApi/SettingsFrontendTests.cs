using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class SettingsFrontendTests
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
    [InlineData("controllers/settings.js")]
    [InlineData("models/settings.js")]
    [InlineData("viewmodels/settings.js")]
    [InlineData("viewmodels/emailsettings.js")]
    [InlineData("viewmodels/loggingsettings.js")]
    [InlineData("viewmodels/opentelemetrysettings.js")]
    [InlineData("views/settings.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Route_IsRegisteredAndLinkedFromAdministration()
    {
        var app = Read("app.js");
        var administration = Read("viewmodels/administration.js");

        Assert.Contains("\"#/settings\": \"controllers/settings\"", app);
        Assert.Contains("name: \"administration.pages.settings\", icon: \"fa-sliders\", href: \"#/settings\", allowed: canReadConfiguration", administration);
        Assert.Contains("session.hasGrant(\"Configuration.Read\")", administration);
    }

    [Fact]
    public void Model_CallsTheSettingsEndpoints()
    {
        var model = Read("models/settings.js");

        Assert.Contains("http.get(\"/settings\")", model);
        Assert.Contains("http.put(filePath(file), { values: values })", model);
        Assert.Contains("http.post(filePath(file, name), { path: fieldPath })", model);
        Assert.Contains("\"/settings/\" + encodeURIComponent(file)", model);
        Assert.Contains("action(file, \"generate\", fieldPath", model);
        Assert.Contains("action(file, \"rotate\", fieldPath", model);
        Assert.Contains("http.get(\"/generator/qrcode/\" + toBase64Url(uri))", model);
        Assert.Contains("configuration_read_only", model);
    }

    [Fact]
    public void View_ShowsOneTabPerFileWithMaskedSecretsAndTheirActions()
    {
        var view = Read("views/settings.html");

        Assert.All(
            new[]
            {
                "foreach: files", "click: $parent.choose", "css: { active: $parent.isSelected($data) }",
                "with: selected", "foreach: sections", "foreach: fields", "text: shown", "text: actionLabel",
                "click: perform", "if: hasAction", "type=\"password\"", "checked: value", "submit: save",
                "click: $root.discard", "with: generated", "html: qrCode", "visible: readOnly", "visible: isDenied"
            },
            binding => Assert.Contains(binding, view));
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void ViewModel_RequiresTheConfigurationGrantsAndConfirmsGeneratingAndRotating()
    {
        var viewModel = Read("viewmodels/settings.js");

        Assert.Contains("session.hasGrant(\"Configuration.Read\")", viewModel);
        Assert.Contains("session.hasGrant(\"Configuration.Manage\")", viewModel);
        Assert.Contains("window.confirm(", viewModel);
        Assert.Contains("settings[kind](self.selected().name, field.path)", viewModel);
        Assert.Contains("settings.save(tab.name, values)", viewModel);
        Assert.Contains("field.sensitive ? \"\" : field.value", viewModel);
    }

    [Fact]
    public void Controller_RendersTheSettingsView()
    {
        var controller = Read("controllers/settings.js");

        Assert.Contains("text!views/settings.html", controller);
        Assert.Contains("viewmodels/settings", controller);
    }

    [Fact]
    public void Model_CallsTheEmailEndpoints()
    {
        var model = Read("models/settings.js");

        Assert.Contains("http.get(\"/settings/email\")", model);
        Assert.Contains("http.put(\"/settings/email\", request)", model);
    }

    [Fact]
    public void ViewModel_ShowsTheEmailEditorInsteadOfRawFieldsForTheEmailTab()
    {
        var viewModel = Read("viewmodels/settings.js");

        Assert.Contains("viewmodels/emailsettings", viewModel);
        Assert.Contains("self.isEmailSelected", viewModel);
        Assert.Contains("self.isFieldTabSelected", viewModel);
        Assert.Contains("self.saveEmail", viewModel);
        Assert.Contains("self.email.load()", viewModel);
    }

    [Fact]
    public void EmailEditor_ChoosesTheProviderAndValidatesTheSmtpFields()
    {
        var editor = Read("viewmodels/emailsettings.js");

        Assert.Contains("settings.loadEmail()", editor);
        Assert.Contains("settings.saveEmail(self.request())", editor);
        Assert.Contains("self.isSmtp", editor);
        Assert.Contains("self.hasValidPort", editor);
        Assert.Contains("self.password() !== \"\" || self.hasStoredPassword()", editor);
        Assert.Contains("templates[template.name] = template.value()", editor);
    }

    [Fact]
    public void View_OffersTheProviderChoiceAndTheSmtpForm()
    {
        var view = Read("views/settings.html");

        Assert.All(
            new[]
            {
                "with: email, submit: saveEmail, visible: isEmailSelected", "options: providers, value: provider",
                "visible: isSmtp", "value: server", "value: port", "checked: ssl", "value: from",
                "options: authenticationTypes, value: authentication", "visible: isBasic", "value: username",
                "type=\"password\"", "value: password", "text: passwordMask", "foreach: templates",
                "enable: canSave", "submit: save, visible: isFieldTabSelected"
            },
            binding => Assert.Contains(binding, view));
    }

    [Fact]
    public void Model_CallsTheLoggingEndpoints()
    {
        var model = Read("models/settings.js");

        Assert.Contains("http.get(filePath(file, \"logging\"))", model);
        Assert.Contains("http.put(filePath(file), request)", model);
    }

    [Fact]
    public void ViewModel_ShowsTheLoggingEditorForTheAppSettingsTabs()
    {
        var viewModel = Read("viewmodels/settings.js");

        Assert.Contains("viewmodels/loggingsettings", viewModel);
        Assert.Contains("self.isLoggingSelected", viewModel);
        Assert.Contains("self.saveAppSettings", viewModel);
        Assert.Contains("self.logging.load(self.selected().name)", viewModel);
    }

    [Fact]
    public void LoggingEditor_ChoosesTheDefaultLevelAndTheLevelsPerSource()
    {
        var editor = Read("viewmodels/loggingsettings.js");

        Assert.Contains("settings.loadLogging(file)", editor);
        Assert.Contains("self.request = function", editor);
        Assert.Contains("self.add = function", editor);
        Assert.Contains("self.remove = function", editor);
        Assert.Contains("self.isValid", editor);
    }

    [Fact]
    public void View_OffersTheLogLevelChoices()
    {
        var view = Read("views/settings.html");

        Assert.All(
            new[]
            {
                "with: logging",
                "options: levels, optionsCaption: $root.text('settings.logging.notSet'), value: defaultLevel",
                "foreach: overrides", "value: source", "options: $parent.levels, value: level",
                "click: $parent.remove", "click: add", "foreach: sources"
            },
            binding => Assert.Contains(binding, view));
    }

    [Fact]
    public void Model_CallsTheOpenTelemetryEndpoints()
    {
        var model = Read("models/settings.js");

        Assert.Contains("http.get(filePath(file, \"opentelemetry\"))", model);
        Assert.Contains("saveAppSettings: function (file, request)", model);
    }

    [Fact]
    public void OpenTelemetryEditor_ValidatesExporterSettings()
    {
        var editor = Read("viewmodels/opentelemetrysettings.js");

        Assert.Contains("settings.loadOpenTelemetry(file)", editor);
        Assert.Contains("self.request = function", editor);
        Assert.Contains("self.protocols(data.protocols)", editor);
        Assert.Contains("self.isValid", editor);
        Assert.Contains("OtlpExporter", editor);
    }

    [Fact]
    public void View_OffersTheOpenTelemetryConfigurationFields()
    {
        var view = Read("views/settings.html");

        Assert.All(
            new[]
            {
                "submit: saveAppSettings, visible: isLoggingSelected", "with: openTelemetry",
                "value: serviceName", "with: traces", "with: metrics", "with: logging",
                "options: $parent.protocols", "foreach: instruments", "checked: selected",
                "checked: includeFormattedMessage", "checked: includeScopes", "checked: parseStateValues"
            },
            binding => Assert.Contains(binding, view));

        var editor = view[view.IndexOf("submit: saveAppSettings", StringComparison.Ordinal)..view.IndexOf("with: selected", StringComparison.Ordinal)];
        Assert.Equal(1, editor.Split("type=\"submit\"", StringSplitOptions.None).Length - 1);
    }
}
