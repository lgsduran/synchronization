using Synchronization.Settings;

namespace Synchronization.Utils
{
    /// <summary>
    /// Class <c>WriteLogeFileUtils</c> utility to save a log file.
    /// </summary>
    public class WriteLogFileUtils
    {
        private static readonly object WriteLock = new();

        /// <summary>
        /// Method <c>WriteFile</c> updates the log file according to the input action.
        /// <exception cref="Exception">
        /// Thrown when the file might be corrupted.
        /// </exception>
        /// </summary>
        public void WriteFile(string action, string fileNames, string srcFolder, string destFolder)
        {
            if (string.IsNullOrWhiteSpace(FolderOptions.LogFilePath))
                throw new InvalidOperationException("The log directory is not configured.");

            var logDirectory = Path.GetFullPath(FolderOptions.LogFilePath);
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, "SynchronizationLog.txt");
            var entry = string.Join(
                Environment.NewLine,
                $"Action: {action}",
                $"Source folder: {srcFolder}",
                $"Destination folder: {destFolder}",
                $"File name(s): {fileNames}",
                $"Timestamp (UTC): {DateTimeOffset.UtcNow:O}",
                string.Empty);

            lock (WriteLock)
                File.AppendAllText(logPath, entry + Environment.NewLine);
        }
    }
}
