using Behind.Common.Templating;

namespace Behind.Tests.Common;

public class SimpleTemplateTests
{
    // ── Construction ────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsNameAndContent()
    {
        var template = new SimpleTemplate("greeting", "Hello {{name}}!");
        Assert.Equal("greeting", template.Name);
        Assert.Equal("Hello {{name}}!", template.Content);
    }

    [Fact]
    public void Constructor_HasNoParametersByDefault()
    {
        var template = new SimpleTemplate("greeting", "Hello {{name}}!");
        Assert.Empty(template.Parameters);
    }

    // ── Render (no parameters) ──────────────────────────────────────────────

    [Fact]
    public void Render_NoParameters_ReturnsContentUnchanged()
    {
        var template = new SimpleTemplate("greeting", "Hello {{name}}!");
        Assert.Equal("Hello {{name}}!", template.Render());
    }

    [Fact]
    public void Render_PlainTextWithoutPlaceholders_ReturnsSameText()
    {
        var template = new SimpleTemplate("plain", "No placeholders here.");
        Assert.Equal("No placeholders here.", template.Render());
    }

    // ── AddParameter / Render ────────────────────────────────────────────────

    [Fact]
    public void AddParameter_ReplacesMatchingPlaceholder()
    {
        var template = new SimpleTemplate("greeting", "Hello {{name}}!")
            .AddParameter(new Parameter("name", "World"));

        Assert.Equal("Hello World!", template.Render());
    }

    [Fact]
    public void AddParameter_MultipleParameters_ReplacesAllPlaceholders()
    {
        var template = new SimpleTemplate("greeting", "{{greeting}}, {{name}}!")
            .AddParameter(new Parameter("greeting", "Hello"))
            .AddParameter(new Parameter("name", "World"));

        Assert.Equal("Hello, World!", template.Render());
    }

    [Fact]
    public void AddParameter_UnmatchedPlaceholder_IsLeftUnrendered()
    {
        var template = new SimpleTemplate("greeting", "Hello {{name}}, today is {{day}}.")
            .AddParameter(new Parameter("name", "World"));

        Assert.Equal("Hello World, today is {{day}}.", template.Render());
    }

    [Fact]
    public void AddParameter_PlaceholderAppearingMultipleTimes_ReplacesEveryOccurrence()
    {
        var template = new SimpleTemplate("repeat", "{{word}} {{word}} {{word}}")
            .AddParameter(new Parameter("word", "echo"));

        Assert.Equal("echo echo echo", template.Render());
    }

    [Fact]
    public void AddParameter_ParameterWithoutMatchingPlaceholder_DoesNotChangeContent()
    {
        var template = new SimpleTemplate("greeting", "Hello!")
            .AddParameter(new Parameter("unused", "value"));

        Assert.Equal("Hello!", template.Render());
    }

    [Fact]
    public void AddParameter_EmptyValue_RemovesPlaceholder()
    {
        var template = new SimpleTemplate("greeting", "Hello {{name}}!")
            .AddParameter(new Parameter("name", ""));

        Assert.Equal("Hello !", template.Render());
    }

    [Fact]
    public void AddParameter_LaterParameterWithSameProperty_DoesNotOverwriteEarlierReplacement()
    {
        // Replacement runs sequentially per parameter: once the first
        // parameter replaces the placeholder text, the second parameter's
        // Replace call has nothing left in the string to match.
        var template = new SimpleTemplate("greeting", "Hello {{name}}!")
            .AddParameter(new Parameter("name", "First"))
            .AddParameter(new Parameter("name", "Second"));

        Assert.Equal("Hello First!", template.Render());
    }

    [Fact]
    public void AddParameter_ReturnsNewInstance_OriginalTemplateHasNoParameters()
    {
        var original = new SimpleTemplate("greeting", "Hello {{name}}!");
        var updated = original.AddParameter(new Parameter("name", "World"));

        Assert.Empty(original.Parameters);
        Assert.Single(updated.Parameters);
        Assert.NotSame(original, updated);
    }

    [Fact]
    public void AddParameter_PreservesNameAndContent()
    {
        var template = new SimpleTemplate("greeting", "Hello {{name}}!")
            .AddParameter(new Parameter("name", "World"));

        Assert.Equal("greeting", template.Name);
        Assert.Equal("Hello {{name}}!", template.Content);
    }

    [Fact]
    public void AddParameter_Chained_AccumulatesParametersInOrder()
    {
        var template = new SimpleTemplate("t", "{{a}}{{b}}{{c}}")
            .AddParameter(new Parameter("a", "1"))
            .AddParameter(new Parameter("b", "2"))
            .AddParameter(new Parameter("c", "3"));

        Assert.Equal(3, template.Parameters.Count());
        Assert.Equal("123", template.Render());
    }
}
