using Behind.Core.Tools;

namespace Behind.Tests.Tools;

public sealed class ReleaseNotesGeneratorTests
{
    [Fact]
    public void Generate_UsesLatestReleaseAndEscapesMarkdownCells()
    {
        const string changelog = """
            [
              {
                "version": "2.0.0",
                "date": "2026-11-11",
                "changes": [
                  { "type": "added", "text": "A | B" },
                  { "type": "fixed", "text": "Line one\nLine two" }
                ]
              },
              { "version": "1.0.0", "date": "2026-11-01", "changes": [] }
            ]
            """;

        string markdown = ReleaseNotesGenerator.Generate(changelog);

        Assert.Contains("## v2.0.0 (2026-11-11)", markdown);
        Assert.Contains("| Added | A \\| B |", markdown);
        Assert.Contains("| Fixed | Line one Line two |", markdown);
        Assert.DoesNotContain("v1.0.0", markdown);
    }

    [Fact]
    public void Generate_SelectsRequestedRelease()
    {
        const string changelog = """
            [
              { "version": "2.0.0", "date": "2026-11-11", "changes": [] },
              { "version": "1.0.0", "date": "2026-11-01", "changes": [] }
            ]
            """;

        string markdown = ReleaseNotesGenerator.Generate(changelog, "1.0.0");

        Assert.Contains("## v1.0.0 (2026-11-01)", markdown);
        Assert.DoesNotContain("v2.0.0", markdown);
    }

    [Fact]
    public void Generate_UnknownVersionThrows()
    {
        const string changelog = """
            [
              { "version": "2.0.0", "date": "2026-11-11", "changes": [] }
            ]
            """;

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => ReleaseNotesGenerator.Generate(changelog, "1.0.0"));

        Assert.Contains("Version '1.0.0' was not found", exception.Message);
    }

    [Fact]
    public void Generate_InvalidChangeThrows()
    {
        const string changelog = """
            [
              {
                "version": "2.0.0",
                "date": "2026-11-11",
                "changes": [{ "type": "added" }]
              }
            ]
            """;

        Assert.Throws<InvalidDataException>(() => ReleaseNotesGenerator.Generate(changelog));
    }
}
