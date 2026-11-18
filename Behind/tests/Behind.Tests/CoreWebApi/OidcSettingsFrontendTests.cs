using System.Diagnostics;

namespace Behind.Tests.CoreWebApi;

public class OidcSettingsFrontendTests
{
    [Fact]
    public async Task SecuritySettingsEditorWorks()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Behind.Tests.csproj")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var script = Path.Combine(directory.FullName, "CoreWebApi", "OidcSettingsFrontend.test.js");
        var frontend = Path.GetFullPath(Path.Combine(directory.FullName, "../../src/Core/Behind.Core.WebApi/Frontend"));
        var start = new ProcessStartInfo("node")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add(frontend);
        using var process = Process.Start(start);
        Assert.NotNull(process);
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, (await output) + (await error));
    }
}
