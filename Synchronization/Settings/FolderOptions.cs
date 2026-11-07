namespace Synchronization.Settings
{
    public class FolderOptions
    {
        public static string SourceFolder { get; set; } = string.Empty;
        public static string DestinationFolder { get; set; } = string.Empty;
        public static string LogFilePath { get; set; } = string.Empty;
        public static bool DryRun { get; set; }
        public static bool AllowEmptySource { get; set; }
        public static string SourceReadyFile { get; set; } = ".sync-ready";
        public static int IntervalSeconds { get; set; } = 15;
    }
}
