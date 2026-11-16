namespace Behind.Tests.Helpers;

public static class EnabledLogger
{
    public static ILogger<T> For<T>()
    {
        var logger = Substitute.For<ILogger<T>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        return logger;
    }
}
