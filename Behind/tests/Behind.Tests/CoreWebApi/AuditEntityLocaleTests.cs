using System.Text.Json;
using Behind.Common.Database;
using Behind.Common.Modules;
using Behind.Contact.WebApi;
using Behind.Core.Domain;
using Behind.Core.WebApi;
using Behind.Forum.WebApi;
using Behind.Quotes.WebApi;
using Behind.Sport.WebApi;
using Behind.Web.Database.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Behind.Tests.CoreWebApi;

public sealed class AuditEntityLocaleTests
{
    [Theory]
    [InlineData("en-US")]
    [InlineData("hu-HU")]
    public void EveryAuditedEntityHasATranslation(string locale)
    {
        var catalog = ModuleCatalog.Create([
            ContactModule.Descriptor, ForumModule.Descriptor, QuotesModule.Descriptor, SportModule.Descriptor
        ]).GetOrElse(ModuleCatalog.Empty);
        using var context = new WebContextSqlite(
            new DbContextOptionsBuilder<WebContextSqlite>().UseSqlite("Data Source=:memory:").Options, catalog);
        var provider = EmbeddedFrontend.CreateFileProvider(null, catalog);
        var bundles = new Dictionary<string, Dictionary<string, string>>();
        foreach (var module in new[] { "core", "contact", "forum", "quotes", "sport" })
        {
            var path = module == "core" ? $"locales/{locale}.json" : $"modules/{module}/locales/{locale}.json";
            var file = provider.GetFileInfo(path);
            Assert.True(file.Exists, path);
            using var stream = file.CreateReadStream();
            using var document = JsonDocument.Parse(stream);
            var translations = new Dictionary<string, string>();
            foreach (var property in document.RootElement.GetProperty("messages").EnumerateObject())
            {
                translations[property.Name] = property.Value.GetString()!;
            }
            bundles.Add(module, translations);
        }

        var missing = context.Model.GetEntityTypes()
            .Where(entity => entity.ClrType != typeof(AuditLogEntry) && entity.FindPrimaryKey() is not null)
            .Select(entity =>
            {
                var name = entity.AuditEntityName;
                var module = entity.ClrType.Namespace switch
                {
                    "Behind.Contact.Domain" => "contact",
                    "Behind.Forum.Domain" => "forum",
                    "Behind.Quotes.Domain" => "quotes",
                    "Behind.Sport.Domain" => "sport",
                    _ => "core"
                };
                return (Name: name, Module: module);
            })
            .Distinct()
            .Where(entity => !bundles[entity.Module].TryGetValue("audit.entities." + entity.Name, out var value) || string.IsNullOrWhiteSpace(value))
            .Select(entity => $"{entity.Module}:{entity.Name}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(missing.Length == 0, $"Missing {locale} translations: {string.Join(", ", missing)}");
    }
}
