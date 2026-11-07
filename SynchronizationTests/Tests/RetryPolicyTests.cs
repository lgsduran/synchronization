using Synchronization.Utils;

namespace SynchronizationTests;

public class RetryPolicyTests
{
    [TestCase(32)]
    [TestCase(33)]
    [TestCase(21)]
    public void IsTransient_RecognizesRetryableWindowsIoErrors(int errorCode)
    {
        var exception = new IOException("transient I/O", unchecked((int)(0x80070000 | errorCode)));

        Assert.That(RetryPolicy.IsTransient(exception), Is.True);
    }

    [Test]
    public void IsTransient_DoesNotRetryDiskFull()
    {
        var exception = new IOException("disk full", unchecked((int)0x80070070));

        Assert.That(RetryPolicy.IsTransient(exception), Is.False);
    }

    [Test]
    public void IsTransient_DoesNotRetryMissingFiles()
    {
        Assert.That(RetryPolicy.IsTransient(new FileNotFoundException()), Is.False);
        Assert.That(RetryPolicy.IsTransient(new DirectoryNotFoundException()), Is.False);
    }

    [Test]
    public void IsTransient_RetriesWhenSourceChangesDuringCopy()
    {
        Assert.That(RetryPolicy.IsTransient(new SourceChangedException("source.txt")), Is.True);
    }
}
