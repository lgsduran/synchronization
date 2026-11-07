using Microsoft.Extensions.Logging;

namespace Synchronization.Logs
{
    /// <summary>
    /// Class <c>ConsoleLog</c> has the methods to print the logs on the console.
    /// </summary>
    public class ConsoleLog
    {
        private static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            builder.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            }));

        private readonly ILogger _logger;

        public ConsoleLog(string className)
        {
            _logger = LoggerFactory.CreateLogger(className);
        }

        /// <summary>
        /// Method <c>info</c> formats and writes an informational log message.
        /// </summary>
        public void Info(string message)
        {
            _logger.LogInformation("{Message}", message);
        }

        /// <summary>
        /// Method <c>error</c> formats and writes an error log message.
        /// </summary>
        public void Error(string message)
        {
            _logger.LogError("{Message}", message);
        }

        public void Error(Exception exception, string message)
        {
            _logger.LogError(exception, "{Message}", message);
        }

        public void Warning(Exception exception, string message)
        {
            _logger.LogWarning(exception, "{Message}", message);
        }
    }
}
