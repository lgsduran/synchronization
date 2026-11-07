using Synchronization.Logs;
using Synchronization.Diagnostics;
using Synchronization.Utils;

namespace Synchronization.Extensions;

public static class OperationExtensions
{
    private static readonly CheckSumUtils CheckSum = new();
    private static readonly WriteLogFileUtils LogFile = new();
    private static readonly ConsoleLog DryRunLog = new("DryRun");
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint
    };

    internal static void ValidatePaths(DirectoryInfo source, DirectoryInfo target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        if (!source.Exists)
            throw new DirectoryNotFoundException($"Source directory does not exist: {source.FullName}");

        EnsureNotReparsePoint(source.FullName);
        if (Directory.Exists(target.FullName))
            EnsureNotReparsePoint(target.FullName);

        var sourcePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(source.FullName));
        var targetPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(target.FullName));
        if (IsSameOrDescendant(sourcePath, targetPath) || IsSameOrDescendant(targetPath, sourcePath))
            throw new ArgumentException("Source and destination directories must not be equal or nested.");
    }

    internal static void ValidateReadyFilePath(DirectoryInfo source, string readyFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(readyFile);
        if (Path.IsPathRooted(readyFile))
            throw new ArgumentException("The ready-file path must be relative to the source directory.", nameof(readyFile));

        var sourcePath = Path.GetFullPath(source.FullName);
        var readyPath = Path.GetFullPath(Path.Combine(sourcePath, readyFile));
        if (!IsSameOrDescendant(readyPath, sourcePath) || Path.GetRelativePath(sourcePath, readyPath) == ".")
            throw new ArgumentException("The ready file must be inside the source directory.", nameof(readyFile));
    }

    internal static void ValidateSourceReady(DirectoryInfo source, string readyFile)
    {
        var readyPath = Path.Combine(source.FullName, readyFile);
        if (!File.Exists(readyPath))
            throw new InvalidOperationException(
                $"Source is not marked ready. Create '{readyFile}' in the source directory after the source is fully mounted and populated.");

        EnsureNotReparsePoint(readyPath);
    }

    internal static void ValidateSourceHasFiles(DirectoryInfo source, bool allowEmptySource, string readyFile)
    {
        if (!allowEmptySource &&
            !EnumerateSourceFiles(source, readyFile).Any())
        {
            throw new InvalidOperationException(
                "The ready source contains no data files. Set Synchronization:AllowEmptySource=true to explicitly allow an empty source.");
        }
    }

    private static IEnumerable<FileInfo> EnumerateSourceFiles(DirectoryInfo source, string readyFile)
    {
        var readyPath = Path.GetFullPath(Path.Combine(source.FullName, readyFile));
        return source.EnumerateFiles("*", EnumerationOptions)
            .Where(file => !string.Equals(
                Path.GetFullPath(file.FullName),
                readyPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
    }

    public static void Copy(
        this DirectoryInfo source,
        DirectoryInfo target,
        bool dryRun = false,
        string readyFile = ".sync-ready",
        bool allowEmptySource = false)
    {
        ValidatePaths(source, target);
        ValidateReadyFilePath(source, readyFile);
        ValidateSourceReady(source, readyFile);
        ValidateSourceHasFiles(source, allowEmptySource, readyFile);
        if (dryRun)
            Plan("create directory", target.FullName);
        else
            target.Create();

        foreach (var sourceDirectory in source.EnumerateDirectories("*", EnumerationOptions))
        {
            var relativePath = Path.GetRelativePath(source.FullName, sourceDirectory.FullName);
            var targetPath = Path.Combine(target.FullName, relativePath);
            EnsureNoReparsePoints(target.FullName, targetPath);

            if (File.Exists(targetPath))
            {
                if (dryRun)
                    Plan("replace file with directory", relativePath);
                else
                    File.Delete(targetPath);
            }

            if (!Directory.Exists(targetPath))
            {
                if (dryRun)
                    Plan("create directory", relativePath);
                else
                    Directory.CreateDirectory(targetPath);
            }
        }

        foreach (var sourceFile in EnumerateSourceFiles(source, readyFile))
        {
            var relativePath = Path.GetRelativePath(source.FullName, sourceFile.FullName);
            var targetPath = Path.Combine(target.FullName, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetPath)!;
            EnsureNoReparsePoints(target.FullName, targetDirectory);
            if (!Directory.Exists(targetDirectory))
            {
                if (dryRun)
                    Plan("create directory", Path.GetRelativePath(target.FullName, targetDirectory));
                else
                    Directory.CreateDirectory(targetDirectory);
            }

            EnsureNoReparsePoints(target.FullName, targetPath);

            if (Directory.Exists(targetPath))
            {
                if (dryRun)
                    Plan("replace directory with file", relativePath);
                else
                    Directory.Delete(targetPath);
            }

            if (!File.Exists(targetPath))
            {
                if (dryRun)
                {
                    Plan("copy file", relativePath);
                    continue;
                }

                AtomicFileCopy.Copy(sourceFile.FullName, targetPath, overwrite: false);
                SynchronizationMetrics.RecordFileCopied();
                LogFile.WriteFile("Copy", sourceFile.Name, sourceFile.DirectoryName ?? "", targetDirectory);
                new ConsoleLog("Copy").Info($"Copied: {relativePath}");
            }
        }
    }

    public static void Update(
        this DirectoryInfo source,
        DirectoryInfo target,
        bool dryRun = false,
        string readyFile = ".sync-ready",
        bool allowEmptySource = false)
    {
        ValidatePaths(source, target);
        ValidateReadyFilePath(source, readyFile);
        ValidateSourceReady(source, readyFile);
        ValidateSourceHasFiles(source, allowEmptySource, readyFile);
        if (dryRun)
            Plan("create directory", target.FullName);
        else
            target.Create();

        foreach (var sourceFile in EnumerateSourceFiles(source, readyFile))
        {
            var relativePath = Path.GetRelativePath(source.FullName, sourceFile.FullName);
            var targetPath = Path.Combine(target.FullName, relativePath);
            EnsureNoReparsePoints(target.FullName, targetPath);

            if (Directory.Exists(targetPath) && !dryRun)
                Directory.Delete(targetPath);

            if (File.Exists(targetPath) &&
                !string.Equals(
                    CheckSum.SHA256CheckSum(sourceFile.FullName),
                    CheckSum.SHA256CheckSum(targetPath),
                    StringComparison.Ordinal))
            {
                if (dryRun)
                {
                    Plan("update file", relativePath);
                    continue;
                }

                AtomicFileCopy.Copy(sourceFile.FullName, targetPath, overwrite: true);
                SynchronizationMetrics.RecordFileUpdated();
                LogFile.WriteFile("Update", sourceFile.Name, sourceFile.DirectoryName ?? "", targetPath);
                new ConsoleLog("Update").Info($"Updated: {relativePath}");
            }
        }
    }

    public static void Delete(
        this DirectoryInfo source,
        DirectoryInfo target,
        bool dryRun = false,
        bool allowEmptySource = false,
        string readyFile = ".sync-ready")
    {
        ValidatePaths(source, target);
        ValidateReadyFilePath(source, readyFile);
        ValidateSourceReady(source, readyFile);
        if (!Directory.Exists(target.FullName))
            return;

        ValidateSourceHasFiles(source, allowEmptySource, readyFile);

        foreach (var targetFile in target.EnumerateFiles("*", EnumerationOptions))
        {
            var relativePath = Path.GetRelativePath(target.FullName, targetFile.FullName);
            var sourcePath = Path.Combine(source.FullName, relativePath);
            EnsureNotReparsePoint(targetFile.FullName);

            if (!File.Exists(sourcePath))
            {
                if (dryRun)
                {
                    Plan("delete file", relativePath);
                    continue;
                }

                File.Delete(targetFile.FullName);
                SynchronizationMetrics.RecordFileDeleted();
                LogFile.WriteFile("Delete", targetFile.Name, source.FullName, targetFile.DirectoryName ?? "");
                new ConsoleLog("Delete").Info($"Deleted file: {relativePath}");
            }
        }

        foreach (var targetDirectory in target.EnumerateDirectories("*", EnumerationOptions)
                     .OrderByDescending(directory => directory.FullName.Length))
        {
            var relativePath = Path.GetRelativePath(target.FullName, targetDirectory.FullName);
            var sourcePath = Path.Combine(source.FullName, relativePath);
            EnsureNotReparsePoint(targetDirectory.FullName);

            if (!Directory.Exists(sourcePath))
            {
                if (dryRun)
                {
                    Plan("delete directory", relativePath);
                    continue;
                }

                Directory.Delete(targetDirectory.FullName);
                SynchronizationMetrics.RecordDirectoryDeleted();
                LogFile.WriteFile("Delete", targetDirectory.FullName, source.FullName, "");
                new ConsoleLog("Delete").Info($"Deleted directory: {relativePath}");
            }
        }
    }

    private static void Plan(string action, string path)
    {
        DryRunLog.Info($"Would {action}: {path}");
    }

    private static bool IsSameOrDescendant(string path, string parent)
    {
        var relativePath = Path.GetRelativePath(parent, path);
        return relativePath == "." ||
               (!Path.IsPathRooted(relativePath) &&
                relativePath != ".." &&
                !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException($"Symbolic links and reparse points are not supported: {path}");
        }
    }

    private static void EnsureNoReparsePoints(string root, string path)
    {
        var relativePath = Path.GetRelativePath(root, path);
        var currentPath = root;
        EnsureNotReparsePoint(currentPath);

        if (relativePath == ".")
            return;

        foreach (var segment in relativePath.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            EnsureNotReparsePoint(currentPath);
        }
    }
}
