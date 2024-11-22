using Microsoft.Extensions.Logging;

namespace Topup.Shared;

public static class LogExtention
{
    public static void Info1(this ILogger logger, string message, params object[] args)
    {
        logger.Log(LogLevel.Information, message, args);
    }
}