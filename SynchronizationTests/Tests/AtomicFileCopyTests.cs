using Synchronization.Utils;

namespace SynchronizationTests;

public class AtomicFileCopyTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"atomic-copy-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Test]
    public void Copy_ReplacesDestinationAndLeavesNoTemporaryFiles()
    {
        var source = Path.Combine(_root, "source.txt");
        var destination = Path.Combine(_root, "destination.txt");
        File.WriteAllText(source, "new content");
        File.WriteAllText(destination, "old content");

        AtomicFileCopy.Copy(source, destination, overwrite: true);

        Assert.That(File.ReadAllText(destination), Is.EqualTo("new content"));
        Assert.That(Directory.GetFiles(_root, "*.tmp"), Is.Empty);
    }

    [Test]
    public void Copy_PreservesExistingDestinationWhenSourceCannotBeRead()
    {
        var destination = Path.Combine(_root, "destination.txt");
        File.WriteAllText(destination, "original content");

        Assert.Throws<FileNotFoundException>(() =>
            AtomicFileCopy.Copy(Path.Combine(_root, "missing.txt"), destination, overwrite: true));

        Assert.That(File.ReadAllText(destination), Is.EqualTo("original content"));
        Assert.That(Directory.GetFiles(_root, "*.tmp"), Is.Empty);
    }

    [Test]
    public void Copy_DoesNotReplaceDestinationWhileSourceIsBeingWritten()
    {
        var source = Path.Combine(_root, "source.txt");
        var destination = Path.Combine(_root, "destination.txt");
        File.WriteAllText(source, "source content");
        File.WriteAllText(destination, "original destination");

        using (new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<IOException>(() =>
                AtomicFileCopy.Copy(source, destination, overwrite: true));
        }

        Assert.That(File.ReadAllText(destination), Is.EqualTo("original destination"));
        Assert.That(Directory.GetFiles(_root, "*.tmp"), Is.Empty);
    }
}
