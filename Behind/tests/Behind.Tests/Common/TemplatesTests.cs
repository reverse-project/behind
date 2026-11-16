using System.Collections;
using Behind.Common.Templating;

namespace Behind.Tests.Common;

public class TemplatesTests
{
    // ── Add(Template) ────────────────────────────────────────────────────────

    [Fact]
    public void Add_Template_MakesItRetrievableByName()
    {
        var templates = new Templates();
        var template = new SimpleTemplate("greeting", "Hello {{name}}!");

        templates.Add(template);

        Assert.True(templates.Get("greeting").Match(() => false, t => t == template));
    }

    [Fact]
    public void Add_Template_Null_ThrowsArgumentNullException()
    {
        var templates = new Templates();

        Assert.Throws<ArgumentNullException>(() => templates.Add((Template)null!));
    }

    [Fact]
    public void Add_Template_MultipleTemplates_AreAllEnumerable()
    {
        var templates = new Templates();
        var first = new SimpleTemplate("first", "A");
        var second = new SimpleTemplate("second", "B");

        templates.Add(first);
        templates.Add(second);

        Assert.Equal([first, second], templates);
    }

    // ── Add(name, content) ───────────────────────────────────────────────────

    [Fact]
    public void Add_NameAndContent_CreatesSimpleTemplate()
    {
        var templates = new Templates();

        templates.Add("greeting", "Hello {{name}}!");

        var template = Assert.IsType<SimpleTemplate>(templates.Single());
        Assert.Equal("greeting", template.Name);
        Assert.Equal("Hello {{name}}!", template.Content);
    }

    [Fact]
    public void Add_NameAndContent_NullName_ThrowsArgumentNullException()
    {
        var templates = new Templates();

        Assert.Throws<ArgumentNullException>(() => templates.Add(null!, "content"));
    }

    [Fact]
    public void Add_NameAndContent_NullContent_ThrowsArgumentNullException()
    {
        var templates = new Templates();

        Assert.Throws<ArgumentNullException>(() => templates.Add("name", null!));
    }

    // ── Remove(name) ─────────────────────────────────────────────────────────

    [Fact]
    public void Remove_Name_RemovesMatchingTemplate()
    {
        var templates = new Templates();
        templates.Add("greeting", "Hello {{name}}!");

        templates.Remove("greeting");

        Assert.Empty(templates);
    }

    [Fact]
    public void Remove_Name_LeavesOtherTemplatesUntouched()
    {
        var templates = new Templates();
        templates.Add("greeting", "Hello!");
        templates.Add("farewell", "Bye!");

        templates.Remove("greeting");

        var remaining = Assert.Single(templates);
        Assert.Equal("farewell", remaining.Name);
    }

    [Fact]
    public void Remove_Name_NonExistentName_DoesNothing()
    {
        var templates = new Templates();
        templates.Add("greeting", "Hello!");

        templates.Remove("unknown");

        Assert.Single(templates);
    }

    [Fact]
    public void Remove_Name_Null_ThrowsArgumentNullException()
    {
        var templates = new Templates();

        Assert.Throws<ArgumentNullException>(() => templates.Remove((string)null!));
    }

    // ── Remove(Template) ─────────────────────────────────────────────────────

    [Fact]
    public void Remove_Template_RemovesExactInstance()
    {
        var templates = new Templates();
        var template = new SimpleTemplate("greeting", "Hello!");
        templates.Add(template);

        templates.Remove(template);

        Assert.Empty(templates);
    }

    [Fact]
    public void Remove_Template_NotAdded_DoesNothing()
    {
        var templates = new Templates();
        var added = new SimpleTemplate("greeting", "Hello!");
        var notAdded = new SimpleTemplate("farewell", "Bye!");
        templates.Add(added);

        templates.Remove(notAdded);

        Assert.Single(templates);
    }

    [Fact]
    public void Remove_Template_Null_ThrowsArgumentNullException()
    {
        var templates = new Templates();

        Assert.Throws<ArgumentNullException>(() => templates.Remove((Template)null!));
    }

    // ── Enumeration ──────────────────────────────────────────────────────────

    [Fact]
    public void GetEnumerator_NoTemplates_YieldsNothing()
    {
        var templates = new Templates();

        Assert.Empty(templates);
    }

    [Fact]
    public void GetEnumerator_Generic_MatchesNonGeneric()
    {
        var templates = new Templates();
        templates.Add("greeting", "Hello!");

        var nonGenericEnumerator = ((IEnumerable)templates).GetEnumerator();
        Assert.True(nonGenericEnumerator.MoveNext());
        Assert.IsType<SimpleTemplate>(nonGenericEnumerator.Current);
        Assert.False(nonGenericEnumerator.MoveNext());
    }

    // ── Get ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Get_ExistingName_ReturnsSomeWithTemplate()
    {
        var templates = new Templates();
        templates.Add("greeting", "Hello!");

        var result = templates.Get("greeting");

        var template = result.Match<Template?>(() => null, t => t);
        Assert.NotNull(template);
        Assert.Equal("greeting", template.Name);
    }

    [Fact]
    public void Get_UnknownName_ReturnsNone()
    {
        var templates = new Templates();

        var result = templates.Get("unknown");

        Assert.True(result.Match(() => true, _ => false));
    }

    [Fact]
    public void Get_DuplicateNames_ThrowsInvalidOperationException()
    {
        var templates = new Templates();
        templates.Add("greeting", "Hello!");
        templates.Add("greeting", "Hi!");

        Assert.Throws<InvalidOperationException>(() => templates.Get("greeting"));
    }

    // ── GetOrDefault ─────────────────────────────────────────────────────────

    [Fact]
    public void GetOrDefault_ExistingName_ReturnsMatchingTemplate()
    {
        var templates = new Templates();
        var template = new SimpleTemplate("greeting", "Hello!");
        templates.Add(template);
        var defaultTemplate = new SimpleTemplate("default", "Default");

        var result = templates.GetOrDefault("greeting", defaultTemplate);

        Assert.Same(template, result);
    }

    [Fact]
    public void GetOrDefault_UnknownName_ReturnsDefaultTemplate()
    {
        var templates = new Templates();
        var defaultTemplate = new SimpleTemplate("default", "Default");

        var result = templates.GetOrDefault("unknown", defaultTemplate);

        Assert.Same(defaultTemplate, result);
    }

    [Fact]
    public void GetOrDefault_NullName_ThrowsArgumentNullException()
    {
        var templates = new Templates();
        var defaultTemplate = new SimpleTemplate("default", "Default");

        Assert.Throws<ArgumentNullException>(() => templates.GetOrDefault(null!, defaultTemplate));
    }

    [Fact]
    public void GetOrDefault_NullDefaultTemplate_ThrowsArgumentNullException()
    {
        var templates = new Templates();

        Assert.Throws<ArgumentNullException>(() => templates.GetOrDefault("name", null!));
    }
}

public class TemplatesConvertersTests
{
    [Fact]
    public void TypeConverter_CreatesTemplatesFromADictionaryAndSkipsBlankContents()
    {
        var values = new Dictionary<string, string?> { ["a"] = "one", ["b"] = " ", ["c"] = null };

        var templates = Assert.IsType<Templates>(
            System.ComponentModel.TypeDescriptor.GetConverter(typeof(Templates)).ConvertFrom(
                null,
                System.Globalization.CultureInfo.InvariantCulture,
                values));

        Assert.Equal([("a", "one")], templates.Select(template => (template.Name, template.Content)));
    }

    [Fact]
    public void TypeConverter_OnlyConvertsFromADictionary()
    {
        var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(Templates));

        Assert.True(converter.CanConvertFrom(typeof(IReadOnlyDictionary<string, string?>)));
        Assert.False(converter.CanConvertFrom(typeof(int)));
        Assert.Throws<NotSupportedException>(() => converter.ConvertFrom(5));
    }

    [Fact]
    public void JsonConverter_WritesAnObjectAndReadsItBack()
    {
        var templates = new Templates { { "a", "one" }, { "b", "<p>two</p>" } };

        var json = System.Text.Json.JsonSerializer.Serialize(templates);
        var back = System.Text.Json.JsonSerializer.Deserialize<Templates>(json)!;

        Assert.Equal("""{"a":"one","b":"\u003Cp\u003Etwo\u003C/p\u003E"}""", json);
        Assert.Equal(
            templates.Select(template => (template.Name, template.Content)),
            back.Select(template => (template.Name, template.Content)));
    }
}
