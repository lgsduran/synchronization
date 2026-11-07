namespace Synchronization.Utils;

public static class AtomicFileCopy
{
    public static void Copy(string sourcePath, string destinationPath, bool overwrite)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
            ?? throw new ArgumentException("Destination must include a directory.", nameof(destinationPath));
        Directory.CreateDirectory(destinationDirectory);

        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var sourceFile = new FileInfo(sourcePath);
                sourceFile.Refresh();
                var initialLength = source.Length;
                var initialWriteTime = sourceFile.LastWriteTimeUtc;
                using var copiedHash = System.Security.Cryptography.IncrementalHash.CreateHash(
                    System.Security.Cryptography.HashAlgorithmName.SHA256);
                var buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    destination.Write(buffer, 0, bytesRead);
                    copiedHash.AppendData(buffer, 0, bytesRead);
                }

                destination.Flush(flushToDisk: true);

                sourceFile.Refresh();
                if (source.Length != initialLength || sourceFile.Length != initialLength ||
                    sourceFile.LastWriteTimeUtc != initialWriteTime)
                {
                    throw new SourceChangedException(sourcePath);
                }

                using var currentSource = new FileStream(
                    sourcePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                var currentHash = System.Security.Cryptography.SHA256.HashData(currentSource);
                if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        copiedHash.GetHashAndReset(),
                        currentHash))
                {
                    throw new SourceChangedException(sourcePath);
                }
            }

            File.Move(temporaryPath, destinationPath, overwrite);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
