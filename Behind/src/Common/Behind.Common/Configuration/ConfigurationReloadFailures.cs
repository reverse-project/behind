namespace Behind.Common.Configuration;

public sealed class ConfigurationReloadFailures
{
    public event Action<string, Exception>? Failed;

    public void Report(string source, Exception exception) => Failed?.Invoke(source, exception);
}
