using Synchronization.Settings;

namespace SynchronizationTests;

public class SettingOptionsTests
{
    [Test]
    public void GetSettings_UsesEnvironmentOverridesAndResolvesRelativePaths()
    {
        const string sourceKey = "Folders__SourceFolder";
        const string destinationKey = "Folders__DestinationFolder";
        const string logKey = "Folders__LogFilePath";
        const string dryRunKey = "Synchronization__DryRun";
        const string allowEmptySourceKey = "Synchronization__AllowEmptySource";
        const string readyFileKey = "Synchronization__SourceReadyFile";
        const string intervalKey = "Synchronization__IntervalSeconds";
        const string environmentKey = "DOTNET_ENVIRONMENT";
        var previousValues = new Dictionary<string, string?>
        {
            [sourceKey] = Environment.GetEnvironmentVariable(sourceKey),
            [destinationKey] = Environment.GetEnvironmentVariable(destinationKey),
            [logKey] = Environment.GetEnvironmentVariable(logKey),
            [dryRunKey] = Environment.GetEnvironmentVariable(dryRunKey),
            [allowEmptySourceKey] = Environment.GetEnvironmentVariable(allowEmptySourceKey),
            [readyFileKey] = Environment.GetEnvironmentVariable(readyFileKey),
            [intervalKey] = Environment.GetEnvironmentVariable(intervalKey),
            [environmentKey] = Environment.GetEnvironmentVariable(environmentKey)
        };

        try
        {
            Environment.SetEnvironmentVariable(environmentKey, "Test");
            Environment.SetEnvironmentVariable(sourceKey, "test-source");
            Environment.SetEnvironmentVariable(destinationKey, "test-destination");
            Environment.SetEnvironmentVariable(logKey, "test-logs");
            Environment.SetEnvironmentVariable(dryRunKey, "true");
            Environment.SetEnvironmentVariable(allowEmptySourceKey, "true");
            Environment.SetEnvironmentVariable(readyFileKey, ".test-ready");
            Environment.SetEnvironmentVariable(intervalKey, "30");

            SettingOptions.GetSettings();

            Assert.That(FolderOptions.SourceFolder, Is.EqualTo(Path.GetFullPath("test-source", AppContext.BaseDirectory)));
            Assert.That(FolderOptions.DestinationFolder, Is.EqualTo(Path.GetFullPath("test-destination", AppContext.BaseDirectory)));
            Assert.That(FolderOptions.LogFilePath, Is.EqualTo(Path.GetFullPath("test-logs", AppContext.BaseDirectory)));
            Assert.That(FolderOptions.DryRun, Is.True);
            Assert.That(FolderOptions.AllowEmptySource, Is.True);
            Assert.That(FolderOptions.SourceReadyFile, Is.EqualTo(".test-ready"));
            Assert.That(FolderOptions.IntervalSeconds, Is.EqualTo(30));
        }
        finally
        {
            foreach (var (key, value) in previousValues)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    [Test]
    public void GetSettings_RejectsInvalidSynchronizationInterval()
    {
        const string intervalKey = "Synchronization__IntervalSeconds";
        var previousValue = Environment.GetEnvironmentVariable(intervalKey);

        try
        {
            Environment.SetEnvironmentVariable(intervalKey, "0");

            Assert.Throws<InvalidOperationException>(SettingOptions.GetSettings);
        }
        finally
        {
            Environment.SetEnvironmentVariable(intervalKey, previousValue);
        }
    }
}
