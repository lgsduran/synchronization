namespace Synchronization.Utils;

public static class RetryPolicy
{
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;
    private const int ErrorDeviceNotReady = 21;
    private const int ErrorInterrupted = 4;
    private const int ErrorTryAgain = 11;
    private const int ErrorBusy = 16;
    private const int ErrorTimedOut = 110;

    public static bool IsTransient(Exception exception)
    {
        if (exception is SourceChangedException)
            return true;

        if (exception is not IOException ||
            exception is FileNotFoundException or DirectoryNotFoundException or
                DriveNotFoundException or PathTooLongException)
        {
            return false;
        }

        var errorCode = exception.HResult & 0xFFFF;
        return errorCode is ErrorSharingViolation or ErrorLockViolation or ErrorDeviceNotReady or
            ErrorInterrupted or ErrorTryAgain or ErrorBusy or ErrorTimedOut;
    }
}

public sealed class SourceChangedException(string path)
    : IOException($"Source file changed while it was being copied: {path}");
