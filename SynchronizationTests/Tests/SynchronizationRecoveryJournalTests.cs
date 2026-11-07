using Synchronization.Utils;

namespace SynchronizationTests;

public class SynchronizationRecoveryJournalTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sync-journal-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void Begin_RecordsRunAndReturnsPreviousIncompleteRun()
    {
        var journal = new SynchronizationRecoveryJournal(_root);
        var source = Path.Combine(_root, "source");
        var destination = Path.Combine(_root, "destination");

        Assert.That(journal.Begin(source, destination), Is.Null);
        var previousRun = journal.Begin(source, destination);

        Assert.That(previousRun, Is.Not.Null);
        Assert.That(previousRun!.SourcePath, Is.EqualTo(Path.GetFullPath(source)));
        Assert.That(previousRun.DestinationPath, Is.EqualTo(Path.GetFullPath(destination)));
        Assert.That(previousRun.StartedAtUtc, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    [Test]
    public void Complete_RemovesRecoveryJournalAfterSuccessfulRun()
    {
        var journal = new SynchronizationRecoveryJournal(_root);
        journal.Begin(Path.Combine(_root, "source"), Path.Combine(_root, "destination"));

        journal.Complete();

        Assert.That(Directory.GetFiles(_root, ".synchronization.pending.json"), Is.Empty);
    }

    [Test]
    public void Begin_ReportsCorruptedJournalInsteadOfSilentlyReplacingIt()
    {
        File.WriteAllText(
            Path.Combine(_root, ".synchronization.pending.json"),
            "not valid json");
        var journal = new SynchronizationRecoveryJournal(_root);

        Assert.Throws<System.Text.Json.JsonException>(() =>
            journal.Begin(Path.Combine(_root, "source"), Path.Combine(_root, "destination")));
    }
}
