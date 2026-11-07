using Synchronization.Operations;
using Synchronization.Settings;
using Synchronization.Utils;
using Synchronization.Diagnostics;
using System.Diagnostics.Metrics;

namespace SynchronizationTests;

public class SynchronizationSafetyTests
{
    private string _root = null!;
    private string _source = null!;
    private string _target = null!;
    private OperationImpl _operation = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"synchronization-tests-{Guid.NewGuid():N}");
        _source = Path.Combine(_root, "source");
        _target = Path.Combine(_root, "target");
        Directory.CreateDirectory(_source);
        File.WriteAllText(Path.Combine(_source, ".sync-ready"), "ready");

        FolderOptions.LogFilePath = Path.Combine(_root, "logs") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(FolderOptions.LogFilePath);
        _operation = new OperationImpl().InitializeOperation(_source, _target);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void Synchronize_CopiesUpdatesAndRemovesStaleNestedFiles()
    {
        Directory.CreateDirectory(Path.Combine(_source, "nested", "deeper"));
        File.WriteAllText(Path.Combine(_source, "nested", "deeper", "shared.txt"), "new content");
        File.WriteAllText(Path.Combine(_source, "root.txt"), "root content");

        Directory.CreateDirectory(Path.Combine(_target, "nested", "deeper"));
        Directory.CreateDirectory(Path.Combine(_target, "stale"));
        File.WriteAllText(Path.Combine(_target, "nested", "deeper", "shared.txt"), "old content");
        File.WriteAllText(Path.Combine(_target, "nested", "deeper", "obsolete.txt"), "obsolete");
        File.WriteAllText(Path.Combine(_target, "stale", "only-here.txt"), "obsolete");

        _operation.Update().Copy().Delete();

        Assert.That(File.ReadAllText(Path.Combine(_target, "nested", "deeper", "shared.txt")), Is.EqualTo("new content"));
        Assert.That(File.ReadAllText(Path.Combine(_target, "root.txt")), Is.EqualTo("root content"));
        Assert.That(File.Exists(Path.Combine(_target, ".sync-ready")), Is.False);
        Assert.That(File.Exists(Path.Combine(_target, "nested", "deeper", "obsolete.txt")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_target, "stale")), Is.False);
    }

    [Test]
    public void InitializeOperation_RejectsEqualOrNestedDirectories()
    {
        Assert.Throws<ArgumentException>(() =>
            new OperationImpl().InitializeOperation(_source, _source));
        Assert.Throws<ArgumentException>(() =>
            new OperationImpl().InitializeOperation(_source, Path.Combine(_source, "nested")));
        Assert.Throws<ArgumentException>(() =>
            new OperationImpl().InitializeOperation(_source, _root));
    }

    [Test]
    public void InitializeOperation_RejectsMissingSourceDirectory()
    {
        Assert.Throws<DirectoryNotFoundException>(() =>
            new OperationImpl().InitializeOperation(Path.Combine(_root, "missing"), _target));
    }

    [Test]
    public void Synchronize_CreatesAnAbsentDestination()
    {
        File.WriteAllText(Path.Combine(_source, "new.txt"), "content");

        _operation.Update().Copy().Delete();

        Assert.That(File.ReadAllText(Path.Combine(_target, "new.txt")), Is.EqualTo("content"));
    }

    [Test]
    public void Copy_RejectsDestinationSymbolicLinks()
    {
        var outsideDirectory = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outsideDirectory);
        Directory.CreateDirectory(_target);
        Directory.CreateSymbolicLink(Path.Combine(_target, "linked"), outsideDirectory);

        Directory.CreateDirectory(Path.Combine(_source, "linked"));
        File.WriteAllText(Path.Combine(_source, "linked", "payload.txt"), "content");

        Assert.Throws<IOException>(() => _operation.Copy());
        Assert.That(File.Exists(Path.Combine(outsideDirectory, "payload.txt")), Is.False);
    }

    [Test]
    public void Delete_DoesNotRecursivelyDeleteThroughSymbolicLinks()
    {
        var outsideDirectory = Path.Combine(_root, "outside");
        var staleDirectory = Path.Combine(_target, "stale");
        Directory.CreateDirectory(outsideDirectory);
        Directory.CreateDirectory(staleDirectory);
        File.WriteAllText(Path.Combine(outsideDirectory, "keep.txt"), "preserve");
        Directory.CreateSymbolicLink(Path.Combine(staleDirectory, "linked"), outsideDirectory);

        var allowEmptySourceOperation = new OperationImpl()
            .InitializeOperation(_source, _target, allowEmptySource: true);
        Assert.Throws<IOException>(() => allowEmptySourceOperation.Delete());
        Assert.That(File.ReadAllText(Path.Combine(outsideDirectory, "keep.txt")), Is.EqualTo("preserve"));
    }

    [Test]
    public void Delete_RefusesToRemoveDestinationWhenSourceHasNoFiles()
    {
        Directory.CreateDirectory(_target);
        var targetFile = Path.Combine(_target, "keep.txt");
        File.WriteAllText(targetFile, "preserve");
        File.Delete(Path.Combine(_source, ".sync-ready"));

        Assert.Throws<InvalidOperationException>(() => _operation.Update().Copy().Delete());

        Assert.That(File.ReadAllText(targetFile), Is.EqualTo("preserve"));
    }

    [Test]
    public void Delete_AllowsEmptySourceWhenExplicitlyEnabled()
    {
        Directory.CreateDirectory(_target);
        var targetFile = Path.Combine(_target, "remove.txt");
        File.WriteAllText(targetFile, "remove");
        var allowEmptySourceOperation = new OperationImpl()
            .InitializeOperation(_source, _target, allowEmptySource: true);

        allowEmptySourceOperation.Delete();

        Assert.That(File.Exists(targetFile), Is.False);
    }

    [Test]
    public void Delete_RequiresReadyFileBeforeRemovingDestinationData()
    {
        Directory.CreateDirectory(_target);
        var targetFile = Path.Combine(_target, "keep.txt");
        File.WriteAllText(targetFile, "preserve");
        File.Delete(Path.Combine(_source, ".sync-ready"));

        Assert.Throws<InvalidOperationException>(() => _operation.Delete());
        Assert.That(File.ReadAllText(targetFile), Is.EqualTo("preserve"));
    }

    [Test]
    public void InitializeOperation_RejectsUnreadySourceBeforeSynchronizing()
    {
        File.Delete(Path.Combine(_source, ".sync-ready"));
        File.WriteAllText(Path.Combine(_source, "partial.txt"), "partial");

        Assert.Throws<InvalidOperationException>(() =>
            new OperationImpl().InitializeOperation(_source, _target));
        Assert.That(File.Exists(Path.Combine(_target, "partial.txt")), Is.False);
    }

    [Test]
    public void InitializeOperation_RejectsReadyFileOutsideSource()
    {
        Assert.Throws<ArgumentException>(() =>
            new OperationImpl().InitializeOperation(_source, _target, sourceReadyFile: "../ready"));
    }

    [Test]
    public void DryRun_LeavesDestinationUnchanged()
    {
        var sourceFile = Path.Combine(_source, "shared.txt");
        var destinationFile = Path.Combine(_target, "shared.txt");
        Directory.CreateDirectory(_target);
        File.WriteAllText(sourceFile, "new content");
        File.WriteAllText(destinationFile, "old content");
        File.WriteAllText(Path.Combine(_target, "stale.txt"), "preserve");
        var dryRunOperation = new OperationImpl()
            .InitializeOperation(_source, _target, dryRun: true);

        dryRunOperation.Update().Copy().Delete();

        Assert.That(File.ReadAllText(destinationFile), Is.EqualTo("old content"));
        Assert.That(File.Exists(Path.Combine(_target, "stale.txt")), Is.True);
    }

    [Test]
    public void DryRun_DoesNotCreateDestination()
    {
        File.WriteAllText(Path.Combine(_source, "new.txt"), "content");
        var dryRunOperation = new OperationImpl()
            .InitializeOperation(_source, _target, dryRun: true);

        dryRunOperation.Update().Copy().Delete();

        Assert.That(Directory.Exists(_target), Is.False);
    }

    [Test]
    public void WriteLogFile_PropagatesLogDirectoryErrors()
    {
        var logPathIsFile = Path.Combine(_root, "not-a-directory");
        File.WriteAllText(logPathIsFile, "file");
        FolderOptions.LogFilePath = logPathIsFile;

        Assert.Throws<IOException>(() =>
            new WriteLogFileUtils().WriteFile("test", "file.txt", _source, _target));
    }

    [Test]
    public void Synchronize_EmitsFileOperationMetrics()
    {
        var measurements = new Dictionary<string, long>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == SynchronizationMetrics.MeterName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            measurements[instrument.Name] = measurements.GetValueOrDefault(instrument.Name) + measurement);
        listener.Start();

        File.WriteAllText(Path.Combine(_source, "copied.txt"), "new");
        Directory.CreateDirectory(_target);
        File.WriteAllText(Path.Combine(_target, "updated.txt"), "old");
        File.WriteAllText(Path.Combine(_source, "updated.txt"), "new");
        File.WriteAllText(Path.Combine(_target, "deleted.txt"), "stale");

        _operation.Update().Copy().Delete();
        listener.RecordObservableInstruments();

        Assert.That(measurements["synchronization.files.copied"], Is.EqualTo(1));
        Assert.That(measurements["synchronization.files.updated"], Is.EqualTo(1));
        Assert.That(measurements["synchronization.files.deleted"], Is.EqualTo(1));
    }
}
