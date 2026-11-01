using System.Text;
using Behind.Core.WebApi;
using Microsoft.Extensions.FileProviders;

namespace Behind.Tests.CoreWebApi;

public class AuditLogFrontendTests
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
    [InlineData("controllers/auditlog.js")]
    [InlineData("viewmodels/auditlog.js")]
    [InlineData("views/auditlog.html")]
    public void Provider_ContainsFile(string path)
    {
        Assert.True(Provider.GetFileInfo(path).Exists);
    }

    [Fact]
    public void Route_IsRegisteredAndLinkedFromAdministration()
    {
        var app = Read("app.js");
        var administration = Read("viewmodels/administration.js");
        var header = Read("viewmodels/header.js");

        Assert.Contains("\"#/audit-log\": \"controllers/auditlog\"", app);
        Assert.Contains("name: \"Audit log\", icon: \"fa-clipboard-list\", href: \"#/audit-log\", allowed: canReadAuditLog", administration);
        Assert.Contains("session.hasGrant(\"AuditLog.Read\")", administration);
        Assert.Contains("session.hasGrant(\"AuditLog.Read\")", header);
    }

    [Fact]
    public void Auth_CallsTheAuditLogEndpoint()
    {
        var auth = Read("models/auth.js");

        Assert.Contains("loadAuditLog: function (query)", auth);
        Assert.Contains("http.get(url)", auth);
        Assert.Contains("\"/auditLog?fromDate=\"", auth);
        Assert.Contains("\"&oldestFirst=\" + query.oldestFirst", auth);
    }

    [Fact]
    public void AuditLogView_OffersFilteringPagingAndTheEntryDetails()
    {
        var view = Read("views/auditlog.html");

        Assert.All(
            new[]
            {
                "submit: find", "value: fromDate", "value: toDate", "value: search", "value: oldestFirst",
                "click: reset", "foreach: entries", "foreach: changes", "visible: canView", "visible: isDenied",
                "<pager class=\"d-block\" params=\"pager: pager\">"
            },
            binding => Assert.Contains(binding, view));
        Assert.DoesNotContain("<!--", view);
    }

    [Fact]
    public void AuditLogViewModel_RequiresTheAuditLogReadGrantAndAValidRange()
    {
        var viewModel = Read("viewmodels/auditlog.js");

        Assert.Contains("session.hasGrant(\"AuditLog.Read\")", viewModel);
        Assert.Contains("self.fromDate() <= self.toDate()", viewModel);
        Assert.Contains("auth.loadAuditLog(", viewModel);
    }
}
