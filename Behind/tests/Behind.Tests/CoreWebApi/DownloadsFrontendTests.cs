using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class DownloadsFrontendTests
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
    [InlineData("controllers/downloads.js")]
    [InlineData("controllers/downloadmanager.js")]
    [InlineData("viewmodels/downloads.js")]
    [InlineData("viewmodels/downloadmanager.js")]
    [InlineData("views/downloads.html")]
    [InlineData("views/downloadmanager.html")]
    [InlineData("models/downloadbrowser.js")]
    [InlineData("models/downloads.js")]
    [InlineData("models/downloadtree.js")]
    [InlineData("models/filetypes.js")]
    [InlineData("components/downloadtree.js")]
    [InlineData("components/downloadtree.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Theory]
    [InlineData("downloads")]
    [InlineData("downloadmanager")]
    public void Controllers_BindTheirViewAndViewModel(string name)
    {
        var controller = Read($"controllers/{name}.js");

        Assert.Contains($"text!views/{name}.html", controller);
        Assert.Contains($"viewmodels/{name}", controller);
        Assert.Contains("createController(view,", controller);
    }

    [Fact]
    public void Routes_ServeTheFolderPathsBeforeTheSiteSlugs()
    {
        var app = Read("app.js");

        Assert.Contains("\"#/downloads\": \"controllers/downloads\"", app);
        Assert.Contains("\"#/manage/downloads\": \"controllers/downloadmanager\"", app);
        Assert.Contains("pattern: /(?:^|\\/)#\\/manage\\/downloads\\/(.*)$/, controller: \"controllers/downloadmanager\"", app);
        Assert.Contains("pattern: /(?:^|\\/)#\\/downloads\\/(.*)$/, controller: \"controllers/downloads\"", app);
        Assert.Contains("ko.components.register(\"download-tree\", { require: \"components/downloadtree\" })", app);
        Assert.True(
            app.IndexOf("patternRoutes.forEach", StringComparison.Ordinal)
            < app.IndexOf("[routes, moduleRouteTable(), siteRoutes].forEach", StringComparison.Ordinal));
    }

    [Fact]
    public void Navigation_LinksToTheDownloadsFromTheBuiltInMenuAndOffersAdministrationToDownloadReaders()
    {
        Assert.True(BuiltInMenus.Main.HasItem("Downloads", "#/downloads", MenuItemKind.Link));
        var administration = Read("viewmodels/administration.js");
        Assert.Contains("href: \"#/manage/downloads\", allowed: canReadDownloads", administration);
        Assert.Contains("session.hasGrant(\"Download.Read\")", administration);
    }

    [Fact]
    public void DownloadsModel_CallsTheDownloadEndpoints()
    {
        var model = Read("models/downloads.js");

        Assert.Contains("http.get(\"/downloads/folders\")", model);
        Assert.Contains("http.get(folderPath(folderId) + \"/files\")", model);
        Assert.Contains("http.get(\"/downloads/deleted\")", model);
        Assert.Contains("\"/downloads/folders/\"", model);
        Assert.Contains("folderPath(parentId) + \"/folders\"", model);
        Assert.Contains("folderPath(id) + \"/delete\"", model);
        Assert.Contains("folderPath(id) + \"/restore\"", model);
        Assert.Contains("folderPath(id) + \"/hard/delete\"", model);
        Assert.Contains("\"/downloads/files/\"", model);
        Assert.Contains("filePath(id) + \"/delete\"", model);
        Assert.Contains("filePath(id) + \"/restore\"", model);
        Assert.Contains("filePath(id) + \"/hard/delete\"", model);
        Assert.Contains("http.postForm(folderPath(folderId) + \"/files\", form)", model);
        Assert.Contains("form.append(\"files\", file)", model);
        Assert.Contains("form.append(\"displayNames\", displayName)", model);
        Assert.Contains("models/results", model);
        Assert.Contains("slug-taken", model);
        Assert.Contains("parent-deleted", model);
        Assert.Contains("folder-deleted", model);
    }

    [Fact]
    public void PublicView_ShowsAFolderTreeNextToATableOfTheFilesOfTheSelectedFolder()
    {
        var view = Read("views/downloads.html");
        var tree = Read("components/downloadtree.html");

        Assert.Contains("<download-tree params=\"nodes: roots\"></download-tree>", view);
        Assert.Contains("<download-tree params=\"nodes: children\"></download-tree>", tree);
        Assert.Contains("fa-folder-open", tree);
        Assert.Contains("fa-folder'", tree);
        Assert.Contains("fa-chevron-down", tree);
        Assert.Contains("fa-chevron-right", tree);
        Assert.Contains("<th scope=\"col\" data-bind=\"t: 'downloads.column.name'\">Name</th>", view);
        Assert.Contains("<th scope=\"col\" class=\"text-end\" data-bind=\"t: 'downloads.column.size'\">Size</th>", view);
        Assert.Contains("<th scope=\"col\" data-bind=\"t: 'downloads.column.uploadedAt'\">Uploaded at</th>", view);
        Assert.Contains("<th scope=\"col\" data-bind=\"t: 'downloads.column.uploadedBy'\">Uploaded by</th>", view);
        Assert.Contains("css: icon", view);
        Assert.Contains("text: size", view);
        Assert.Contains("text: uploadedAt", view);
        Assert.Contains("text: uploadedBy", view);
        Assert.Contains("attr: { href: url, download: fileName }", view);
    }

    [Fact]
    public void PublicView_HasNoManagementTools_OnlyALinkToTheManagementPageForDownloadReaders()
    {
        var view = Read("views/downloads.html");
        var viewModel = Read("viewmodels/downloads.js");

        Assert.DoesNotContain("type=\"file\"", view);
        Assert.DoesNotContain("click: ", view);
        Assert.DoesNotContain("submit: ", view);
        Assert.DoesNotContain("showDeleted", view);
        Assert.Contains("visible: canManage() && found(), attr: { href: manageHref }", view);
        Assert.Contains("session.hasGrant(\"Download.Read\")", viewModel);
        Assert.Contains("\"#/manage/downloads/\" + browser.selected().path", viewModel);
        Assert.Contains("rootHref: \"#/downloads\"", viewModel);
        Assert.DoesNotContain("downloads.upload", viewModel);
    }

    [Fact]
    public void ManagerView_OffersFolderManagementUploadAndDeletedRecordsOnlyToThoseAllowedToUseThem()
    {
        var view = Read("views/downloadmanager.html");

        Assert.Contains("<download-tree params=\"nodes: browser.roots\"></download-tree>", view);
        Assert.Contains("visible: !isSignedIn()", view);
        Assert.Contains("visible: isDenied", view);
        Assert.Contains("href=\"#/administration\"", view);
        Assert.Contains("visible: canWrite, enable: !busy(), click: startCreateFolder", view);
        Assert.Contains("visible: canWrite() && browser.inFolder(), enable: !busy(), click: startEditFolder", view);
        Assert.Contains("visible: canDelete() && browser.inFolder(), enable: !busy(), click: deleteFolder", view);
        Assert.Contains("type=\"file\" multiple", view);
        Assert.Contains("event: { change: addFiles }", view);
        Assert.Contains("value: name, valueUpdate: 'input'", view);
        Assert.Contains("click: upload", view);
        Assert.Contains("data-bind=\"visible: canViewDeleted\"", view);
        Assert.Contains("checked: showDeleted", view);
        Assert.Contains("click: restore", view);
        Assert.Contains("click: hardDelete", view);
        Assert.Contains("submit: rename", view);
        Assert.Contains("click: remove", view);
    }

    [Fact]
    public void Browser_ResolvesTheFolderPathOfTheUrlAndKeepsTheOpenedFolders()
    {
        var browser = Read("models/downloadbrowser.js");

        Assert.Contains("config.params", browser);
        Assert.Contains("params.splat", browser);
        Assert.Contains("tree.find(folderIndex.roots, requestedPath)", browser);
        Assert.Contains("config.rootHref + \"/\" + node.path", browser);
        Assert.Contains("expanded[view.id] = view.expanded()", browser);
    }

    [Fact]
    public void ManagerViewModel_WorksUnderTheManagementAddressAndChecksTheGrants()
    {
        var viewModel = Read("viewmodels/downloadmanager.js");

        Assert.Contains("rootHref: \"#/manage/downloads\"", viewModel);
        Assert.Contains("session.hasGrant(\"Download.Read\")", viewModel);
        Assert.Contains("session.hasGrant(\"Download.Write\")", viewModel);
        Assert.Contains("session.hasGrant(\"Download.Delete\")", viewModel);
        Assert.Contains("session.hasGrant(\"RawDatabase.Manage\")", viewModel);
    }

    [Fact]
    public void ManagerViewModel_UploadsEveryChosenFileUnderItsDisplayNameThatDefaultsToTheFileName()
    {
        var viewModel = Read("viewmodels/downloadmanager.js");

        Assert.Contains("name: ko.observable(file.name)", viewModel);
        Assert.Contains("downloads.upload(current.id, item.file, item.name().trim())", viewModel);
        Assert.Contains("file.size > browser.maxFileBytes()", viewModel);
    }

    [Fact]
    public void FileTypes_MapTheExtensionsToIconsAndFallBackToAGenericFileIcon()
    {
        var model = Read("models/filetypes.js");

        Assert.Contains("unknown = \"fa-file\"", model);
        Assert.Contains("\"fa-file-pdf\": [\"pdf\"]", model);
        Assert.Contains("\"fa-file-zipper\"", model);
        Assert.Contains("prefix: \"image/\"", model);
    }

    [Fact]
    public void Css_StylesTheFolderTree()
    {
        var css = Read("css/app.css");

        Assert.Contains(".download-tree-selected", css);
        Assert.Contains(".download-tree-toggle", css);
    }
}
