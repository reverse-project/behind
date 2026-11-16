using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Behind.Common.Logging;

[SuppressMessage(
    "Usage",
    "CA2254:Template should be a static expression",
    Justification = "Templates are forwarded unchanged from constant call-site arguments")]
public static class LoggingExtensions
{
    extension(ILogger logger)
    {
        public void Log(LogLevel level, string message, Func<object?[]> arguments)
        {
            if (logger.IsEnabled(level))
            {
                logger.Log(level, message, arguments());
            }
        }

        public void Log(LogLevel level, Exception exception, string message, Func<object?[]> arguments)
        {
            if (logger.IsEnabled(level))
            {
                logger.Log(level, exception, message, arguments());
            }
        }

        public void LogDebug(string message, Func<object?[]> arguments) =>
            logger.Log(LogLevel.Debug, message, arguments);

        public void LogInformation(string message, Func<object?[]> arguments) =>
            logger.Log(LogLevel.Information, message, arguments);

        public void LogWarning(string message, Func<object?[]> arguments) =>
            logger.Log(LogLevel.Warning, message, arguments);

        public void LogWarning(Exception exception, string message, Func<object?[]> arguments) =>
            logger.Log(LogLevel.Warning, exception, message, arguments);

        public void LogError(string message, Func<object?[]> arguments) =>
            logger.Log(LogLevel.Error, message, arguments);

        public void LogError(Exception exception, string message, Func<object?[]> arguments) =>
            logger.Log(LogLevel.Error, exception, message, arguments);
    }
}
