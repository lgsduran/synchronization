using System.Diagnostics.Metrics;

namespace Synchronization.Diagnostics;

public static class SynchronizationMetrics
{
    public const string MeterName = "Synchronization";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> FilesCopied = Meter.CreateCounter<long>("synchronization.files.copied");
    private static readonly Counter<long> FilesUpdated = Meter.CreateCounter<long>("synchronization.files.updated");
    private static readonly Counter<long> FilesDeleted = Meter.CreateCounter<long>("synchronization.files.deleted");
    private static readonly Counter<long> DirectoriesDeleted = Meter.CreateCounter<long>("synchronization.directories.deleted");
    private static readonly Counter<long> JobsCompleted = Meter.CreateCounter<long>("synchronization.jobs.completed");
    private static readonly Counter<long> JobsFailed = Meter.CreateCounter<long>("synchronization.jobs.failed");
    private static readonly Histogram<double> JobDuration = Meter.CreateHistogram<double>(
        "synchronization.job.duration",
        unit: "ms");

    public static void RecordFileCopied() => FilesCopied.Add(1);
    public static void RecordFileUpdated() => FilesUpdated.Add(1);
    public static void RecordFileDeleted() => FilesDeleted.Add(1);
    public static void RecordDirectoryDeleted() => DirectoriesDeleted.Add(1);
    public static void RecordJobCompleted() => JobsCompleted.Add(1);
    public static void RecordJobFailed() => JobsFailed.Add(1);
    public static void RecordJobDuration(TimeSpan duration) => JobDuration.Record(duration.TotalMilliseconds);
}
