using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class SiteSectionsFrontendTests
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
    [InlineData("models/sitesections.js")]
    [InlineData("viewmodels/sectionseditor.js")]
    [InlineData("components/sectionseditor.js")]
    [InlineData("components/sectionseditor.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void App_RegistersTheSectionsEditorComponent()
    {
        Assert.Contains(
            "ko.components.register(\"site-sections-editor\", { require: \"components/sectionseditor\" });",
            Read("app.js"));
    }

    [Fact]
    public void SiteEditor_EditsTheSectionsInTheSameFormAsTheContent()
    {
        var view = Read("views/siteeditor.html");

        Assert.Contains(
            "<site-sections-editor params=\"sections: sections, canWrite: canWrite, upload: uploadImage, maxSections: maxSections\"></site-sections-editor>",
            view);
        Assert.True(
            view.IndexOf("richEditor: { value: content", StringComparison.Ordinal)
            < view.IndexOf("<site-sections-editor", StringComparison.Ordinal));
        Assert.True(
            view.IndexOf("<site-sections-editor", StringComparison.Ordinal)
            < view.IndexOf("click: publish", StringComparison.Ordinal));
    }

    [Fact]
    public void SiteEditor_SavesTheSectionsAndRefusesToSaveEmptyOnes()
    {
        var viewModel = Read("viewmodels/siteeditor.js");

        Assert.Contains("sections: self.sections().map(sectionsModel.toRequest)", viewModel);
        Assert.Contains("self.sections(result.data.sections.map(sectionsModel.editable))", viewModel);
        Assert.Contains("self.maxSections(result.data.maxSections)", viewModel);
        Assert.Contains("!self.hasEmptySection()", viewModel);
        Assert.Contains("sections: fields.sections || []", Read("models/sites.js"));
    }

    [Fact]
    public void PublicSitePage_ShowsTheSectionsBelowTheContent()
    {
        var view = Read("views/site.html");
        var viewModel = Read("viewmodels/site.js");

        Assert.Contains("foreach: sections", view);
        Assert.Contains("attr: { class: cssClass }", view);
        Assert.Contains("attr: { src: imageUrl, alt: imageAlt }", view);
        Assert.True(
            view.IndexOf("html: content\"", StringComparison.Ordinal)
            < view.IndexOf("foreach: sections", StringComparison.Ordinal));
        Assert.Contains("self.sections(result.data.sections.map(sectionsModel.present))", viewModel);
    }

    [Fact]
    public void Model_OffersTheNormalAndAccentedThemesAndBothImagePositions()
    {
        var model = Read("models/sitesections.js");
        var view = Read("components/sectionseditor.html");

        Assert.Contains("var themes = [\"Normal\", \"Accented\"];", model);
        Assert.Contains("var positions = [\"Left\", \"Right\"];", model);
        Assert.Contains("\"behind-section-\" + data.theme.toLowerCase()", model);
        Assert.Contains("\"behind-section-image-\" + data.imagePosition.toLowerCase()", model);
        Assert.Contains("<option value=\"Normal\"", view);
        Assert.Contains("<option value=\"Accented\"", view);
        Assert.Contains("<option value=\"Left\"", view);
        Assert.Contains("<option value=\"Right\"", view);
    }

    [Fact]
    public void Editor_CanAddMoveRemoveSectionsAndUploadPictures()
    {
        var view = Read("components/sectionseditor.html");
        var viewModel = Read("viewmodels/sectionseditor.js");

        Assert.Contains("click: add", view);
        Assert.Contains("click: $parent.moveUp", view);
        Assert.Contains("click: $parent.moveDown", view);
        Assert.Contains("click: $parent.remove", view);
        Assert.Contains("richEditor: { value: content, upload: $parent.upload", view);
        Assert.Contains("event: { change: $parent.pickImage }", view);
        Assert.Contains("sites.uploadImage(file)", viewModel);
        Assert.Contains("self.maxSections()", viewModel);
    }

    [Fact]
    public void Css_PlacesThePictureNextToTheTextAndThemesAccentedSections()
    {
        var css = Read("css/app.css");

        Assert.Contains(".behind-section-accented", css);
        Assert.Contains("--behind-section-accent-bg: var(--bs-primary);", css);
        Assert.Contains(".behind-section-image-right .behind-section-image", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr));", css);
    }

    [Fact]
    public void AuditLog_NamesTheSiteSectionEntity()
    {
        Assert.Contains("SiteSection: \"audit.entities.SiteSection\"", Read("viewmodels/auditlog.js"));
    }

    [Fact]
    public void Locales_TranslateEverySectionKeyInBothLanguages()
    {
        var sources = string.Concat(
            Read("components/sectionseditor.html"),
            Read("viewmodels/sectionseditor.js"),
            Read("viewmodels/siteeditor.js"),
            Read("views/siteeditor.html"));
        var keys = Regex
            .Matches(sources, "[\"'](sections\\.[A-Za-z0-9.]+|sites\\.editor\\.emptySection)[\"']")
            .Select(match => match.Groups[1].Value)
            .Concat(["audit.entities.SiteSection"])
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);

        foreach (var locale in new[] { "en-US", "hu-HU" })
        {
            using var document = JsonDocument.Parse(Read($"locales/{locale}.json"));
            var messages = document.RootElement.GetProperty("messages");

            foreach (var key in keys)
            {
                Assert.True(messages.TryGetProperty(key, out var value) && value.GetString()?.Length > 0, $"{locale}: {key}");
            }
        }
    }
}
