using System;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Logging
{
    public static class LoggingConfigExtensions
    {
        public static LoggingConfigDto GetLoggingConfig(this IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            var serviceConfig = new LoggingConfigDto
            {
                Url = configuration["LoggingConfig:Url"],
                LogFileUrl = configuration["LoggingConfig:LogFileUrl"],
                OutputTemplate = configuration["LoggingConfig:OutputTemplate"],
                RetainedFileCountLimit = configuration["LoggingConfig:RetainedFileCountLimit"],
                IndexFormat = configuration["LoggingConfig:IndexFormat"],
                Application = configuration["LoggingConfig:Application"],
                AutoRegisterTemplate = bool.Parse(configuration["LoggingConfig:AutoRegisterTemplate"]),
            };
            return serviceConfig;
        }
    }
}
