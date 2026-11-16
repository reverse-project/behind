using System.Text;
using System.Text.Json;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class SchedulesFrontendTests
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
    [InlineData("controllers/schedules.js")]
    [InlineData("controllers/scheduleruns.js")]
    [InlineData("viewmodels/schedules.js")]
    [InlineData("viewmodels/scheduleruns.js")]
    [InlineData("views/schedules.html")]
    [InlineData("views/scheduleruns.html")]
    [InlineData("models/schedules.js")]
    [InlineData("models/cronfields.js")]
    public void Provider_ContainsFiles(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Routes_IncludeSchedulePagesBeforeTheAliasRoute()
    {
        var app = Read("app.js");
        var alias = app.IndexOf("var aliasRoute = \"#/:slug\"", StringComparison.Ordinal);

        Assert.Contains("\"#/manage/schedules\": \"controllers/schedules\"", app);
        Assert.Contains("\"#/manage/schedules/runs\": \"controllers/scheduleruns\"", app);
        Assert.True(app.IndexOf("\"#/manage/schedules/runs\"", StringComparison.Ordinal) < alias);
    }

    [Fact]
    public void Model_UsesScheduleAndRunContracts()
    {
        var model = Read("models/schedules.js");

        Assert.Contains("/schedules/manage", model);
        Assert.Contains("/schedules/actions", model);
        Assert.Contains("/schedules/cron?", model);
        Assert.Contains("/schedules/participants?", model);
        Assert.Contains("/schedules/runs?", model);
        Assert.Contains("/hard/delete", model);
        Assert.Contains("/run", model);
        Assert.Contains("already-running", model);
    }

    [Fact]
    public void ManagementPages_ApplyScheduleGrantsAndExposeSoftDeleteWorkflow()
    {
        var viewModel = Read("viewmodels/schedules.js");
        var runsViewModel = Read("viewmodels/scheduleruns.js");
        var view = Read("views/schedules.html");
        var runsView = Read("views/scheduleruns.html");

        Assert.Contains("session.hasGrant(\"Schedule.Read\")", viewModel);
        Assert.Contains("session.hasGrant(\"Schedule.Write\")", viewModel);
        Assert.Contains("session.hasGrant(\"Schedule.Delete\")", viewModel);
        Assert.Contains("session.hasGrant(\"Schedule.Manage\")", viewModel);
        Assert.Contains("session.hasGrant(\"RawDatabase.Manage\")", viewModel);
        Assert.Contains("click: remove", view);
        Assert.Contains("click: restore", view);
        Assert.Contains("click: hardDelete", view);
        Assert.Contains("letterPager", view);
        Assert.Contains("click: run", view);
        Assert.Contains("session.hasGrant(\"Schedule.Delete\")", runsViewModel);
        Assert.Contains("click: remove", runsView);
        Assert.Contains("click: restore", runsView);
        Assert.Contains("click: hardDelete", runsView);
    }

    [Fact]
    public void CronEditor_UsesUtcPreviewAndTheSharedCronFieldModel()
    {
        var viewModel = Read("viewmodels/schedules.js");
        var view = Read("views/schedules.html");

        Assert.Contains("models/cronfields", Read("viewmodels/schedules.js"));
        Assert.Contains("schedules.cron(expression, 5)", viewModel);
        Assert.Contains("request !== cronRequest", viewModel);
        Assert.Contains("cronfields.compose(values)", viewModel);
        Assert.Contains("cronfields.parse(expression)", viewModel);
        Assert.Contains("data-bind=\"foreach: fields\"", view);
        Assert.Contains("schedules.cron.next", view);
    }

    [Fact]
    public void Administration_OffersSchedulesToScheduleReadersAndLocalizesAuditEntities()
    {
        var administration = Read("viewmodels/administration.js");
        var english = Read("locales/en-US.json");

        Assert.Contains("href: \"#/manage/schedules\", allowed: canReadSchedules", administration);
        Assert.Contains("session.hasGrant(\"Schedule.Read\")", administration);
        Assert.Contains("\"audit.entities.Schedule\"", english);
        Assert.Contains("\"audit.entities.ScheduleRun\"", english);
    }

    [Fact]
    public void ParameterEditor_RendersOnlyTheInputOfEachParameterKindWithItsBounds()
    {
        var viewModel = Read("viewmodels/schedules.js");
        var view = Read("views/schedules.html");

        Assert.Contains("<!-- ko if: kind === 'number' -->", view);
        Assert.Contains("<!-- ko if: kind === 'choice' -->", view);
        Assert.DoesNotContain("visible: kind === 'choice'", view);
        Assert.DoesNotContain("visible: kind === 'number'", view);
        Assert.Contains("min: minimum, max: maximum", view);
        Assert.Contains("minimum: parameter.minimum", viewModel);
        Assert.Contains("maximum: parameter.maximum", viewModel);
        Assert.Contains("\"session-cleanup-invalid-age\": \"schedules.conflict.sessionCleanupInvalidAge\"", Read("models/schedules.js"));
    }

    [Fact]
    public void EditorForm_KeepsTheSaveButtonInsideASingleUnvalidatedForm()
    {
        var view = Read("views/schedules.html");
        var start = view.IndexOf("submit: save", StringComparison.Ordinal);
        var end = view.IndexOf("</form>", start, StringComparison.Ordinal);
        var editor = view[start..end];

        Assert.Contains("novalidate data-bind=\"visible: canWrite, submit: save\"", view);
        Assert.DoesNotContain("<form", editor);
        Assert.Contains("enable: canSave", editor);
        Assert.Contains("event: { keydown: emailKeyDown }", editor);
        Assert.Contains("self.emailKeyDown", Read("viewmodels/schedules.js"));
    }

    [Fact]
    public void Save_KeepsTheSavedScheduleOpenAndReportsTheResultNextToTheButton()
    {
        var view = Read("views/schedules.html");
        var viewModel = Read("viewmodels/schedules.js");
        var save = viewModel[viewModel.IndexOf("self.save = function", StringComparison.Ordinal)..viewModel.IndexOf("self.preset = function", StringComparison.Ordinal)];

        Assert.Contains("role=\"status\" data-bind=\"visible: saveMessage, text: saveMessage", view);
        Assert.Contains("self.editingVersion(result.data.version);", save);
        Assert.Contains("self.saveMessage(result.message);", save);
        Assert.DoesNotContain("self.reset()", save);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void Locales_ContainScheduleMessages(string locale)
    {
        using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));
        var messages = document.RootElement.GetProperty("messages");

        foreach (var key in new[]
        {
            "administration.pages.schedules", "audit.entities.Schedule", "audit.entities.ScheduleRun",
            "schedules.title", "schedules.cron.expression", "schedules.cron.calculator", "schedules.notify.label",
            "schedules.participants.title", "schedules.runs.title", "schedules.conflict.invalidCron",
            "schedules.error.runAlreadyRunning",
            "schedules.conflict.sessionCleanupInvalidAge", "schedules.sessionCleanup.action",
            "schedules.sessionCleanup.invalidatedAgeDays"
        })
        {
            Assert.True(messages.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()), key);
        }
    }
}
