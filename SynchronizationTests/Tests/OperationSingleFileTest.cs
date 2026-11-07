using Synchronization.Operations;
using Synchronization.Settings;

namespace SynchronizationTests;

public class OperationSingleFileTest
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
    public void CopySingleFileTest()
    {
        File.WriteAllText(Path.Combine(_source, "test_1454.txt"), "Copy");

        _operation.Copy();

        Assert.That(File.ReadAllText(Path.Combine(_target, "test_1454.txt")), Is.EqualTo("Copy"));
    }

    [Test]
    public void CopyUpdatedSingleFileTest()
    {
        File.WriteAllText(Path.Combine(_source, "test_1454.txt"), "Update");
        Directory.CreateDirectory(_target);
        File.WriteAllText(Path.Combine(_target, "test_1454.txt"), "Original");

        _operation.Update();

        Assert.That(File.ReadAllText(Path.Combine(_target, "test_1454.txt")), Is.EqualTo("Update"));
    }

    [Test]
    public void DeleteSingleFileTest()
    {
        Directory.CreateDirectory(_target);
        File.WriteAllText(Path.Combine(_target, "test_1454.txt"), "Delete");

        _operation = new OperationImpl().InitializeOperation(_source, _target, allowEmptySource: true);
        _operation.Delete();

        Assert.That(File.Exists(Path.Combine(_target, "test_1454.txt")), Is.False);
    }
}
