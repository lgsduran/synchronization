using Microsoft.Extensions.Configuration;
using Synchronization.Logs;

namespace Synchronization.Settings
{
    public class SettingOptions
    {
        private static ConsoleLog _log = new ConsoleLog("Settings");
        /// <summary>
        /// Method <c>GetSettings</c> loads application configuration properties.
        /// </summary>
        public static void GetSettings()
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile($"appsettings.{environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            FolderOptions.SourceFolder = GetRequiredPath(config, "Folders:SourceFolder");
            FolderOptions.DestinationFolder = GetRequiredPath(config, "Folders:DestinationFolder");
            FolderOptions.LogFilePath = GetRequiredPath(config, "Folders:LogFilePath");
            FolderOptions.DryRun = config.GetValue("Synchronization:DryRun", false);
            FolderOptions.AllowEmptySource = config.GetValue("Synchronization:AllowEmptySource", false);
            FolderOptions.SourceReadyFile = config["Synchronization:SourceReadyFile"] ?? ".sync-ready";
            if (Path.IsPathRooted(FolderOptions.SourceReadyFile) ||
                FolderOptions.SourceReadyFile.Split(
                    [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                    StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Synchronization:SourceReadyFile must be a relative path inside the source directory.");
            }

            FolderOptions.IntervalSeconds = config.GetValue("Synchronization:IntervalSeconds", 15);
            if (FolderOptions.IntervalSeconds is < 1 or > 86400)
                throw new InvalidOperationException("Synchronization:IntervalSeconds must be between 1 and 86400.");

            _log.Info("appsettings file is loaded properly.");
        }

        private static string GetRequiredPath(IConfiguration configuration, string key)
        {
            var value = configuration[key];
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Required setting '{key}' is missing or empty.");

            return Path.GetFullPath(value, AppContext.BaseDirectory);
        }
    }
}
