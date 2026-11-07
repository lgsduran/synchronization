using Synchronization.Operations;
using Synchronization.Settings;

namespace SynchronizationTests;

public class OperationSingleFileUnderFolderTest
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
    public void CopySingleFileToFolderTest()
    {
        var sourceFile = Path.Combine(_source, "nested", "test_1454.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        File.WriteAllText(sourceFile, "Copy");

        _operation.Copy();

        Assert.That(
            File.ReadAllText(Path.Combine(_target, "nested", "test_1454.txt")),
            Is.EqualTo("Copy"));
    }

    [Test]
    public void CopyUpdatedFileToFolderTest()
    {
        var relativePath = Path.Combine("nested", "test_1454.txt");
        var sourceFile = Path.Combine(_source, relativePath);
        var targetFile = Path.Combine(_target, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(sourceFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
        File.WriteAllText(sourceFile, "Updated folder");
        File.WriteAllText(targetFile, "Original");

        _operation.Update();

        Assert.That(File.ReadAllText(targetFile), Is.EqualTo("Updated folder"));
    }

    [Test]
    public void ValidateRootFilesAreEqualTest()
    {
        Directory.CreateDirectory(_target);
        File.WriteAllText(Path.Combine(_source, "test_1454.txt"), "same content");
        File.WriteAllText(Path.Combine(_target, "test_1454.txt"), "same content");

        var sourceContent = File.ReadAllText(Path.Combine(_source, "test_1454.txt"));
        var targetContent = File.ReadAllText(Path.Combine(_target, "test_1454.txt"));

        Assert.That(sourceContent, Is.EqualTo(targetContent));
    }

    [Test]
    public void DeleteFileFromFolderTest()
    {
        var targetFile = Path.Combine(_target, "nested", "test_1454.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
        File.WriteAllText(targetFile, "Delete");

        _operation = new OperationImpl().InitializeOperation(_source, _target, allowEmptySource: true);
        _operation.Delete();

        Assert.That(File.Exists(targetFile), Is.False);
    }

    [Test]
    public void DeleteSourceFolderTest()
    {
        var targetDirectory = Path.Combine(_target, "nested");
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, "test_1454.txt"), "Delete");

        _operation = new OperationImpl().InitializeOperation(_source, _target, allowEmptySource: true);
        _operation.Delete();

        Assert.That(Directory.Exists(targetDirectory), Is.False);
    }
}
