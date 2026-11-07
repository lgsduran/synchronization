using System.Text.Json;

namespace Synchronization.Utils;

public sealed class SynchronizationRecoveryJournal
{
    private const string JournalFileName = ".synchronization.pending.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _journalPath;

    public SynchronizationRecoveryJournal(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _journalPath = Path.Combine(Path.GetFullPath(logDirectory), JournalFileName);
    }

    public RecoveryRecord? Begin(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_journalPath)!);

        RecoveryRecord? previousRun = null;
        if (File.Exists(_journalPath))
        {
            var content = File.ReadAllText(_journalPath);
            previousRun = JsonSerializer.Deserialize<RecoveryRecord>(content, JsonOptions)
                ?? throw new InvalidDataException($"Recovery journal is empty or invalid: {_journalPath}");
        }

        var currentRun = new RecoveryRecord(
            Path.GetFullPath(sourcePath),
            Path.GetFullPath(destinationPath),
            DateTimeOffset.UtcNow);
        WriteAtomically(currentRun);
        return previousRun;
    }

    public void Complete()
    {
        File.Delete(_journalPath);
    }

    private void WriteAtomically(RecoveryRecord record)
    {
        var temporaryPath = $"{_journalPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            var content = JsonSerializer.SerializeToUtf8Bytes(record, JsonOptions);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(content);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, _journalPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}

public sealed record RecoveryRecord(
    string SourcePath,
    string DestinationPath,
    DateTimeOffset StartedAtUtc);
