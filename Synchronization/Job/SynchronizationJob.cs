using Quartz;
using Synchronization.Diagnostics;
using Synchronization.Logs;
using Synchronization.Operations;
using Synchronization.Settings;
using Synchronization.Utils;

namespace Synchronization.Job
{
    /// <summary>
    /// Class <c>SynchronizationJob</c> gathers the cycle of operation
    /// that has the job to be performed.
    /// </summary>
    [DisallowConcurrentExecution]
    public class SynchronizationJob : IJob
    {
        /// <summary>
        /// Method <c>Execute</c> gathers the cycle of operation
        /// that is triggered by the job.
        /// </summary>
        /// <returns>
        /// The successfully completed task.
        /// </returns>
        public async Task Execute(IJobExecutionContext context)
        {
            var _log = new ConsoleLog("SynchronizationJob");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            const int maxAttempts = 3;
            var journal = FolderOptions.DryRun
                ? null
                : new SynchronizationRecoveryJournal(FolderOptions.LogFilePath);

            try
            {
                if (journal is not null)
                {
                    var previousRun = journal.Begin(
                        FolderOptions.SourceFolder,
                        FolderOptions.DestinationFolder);
                    if (previousRun is not null)
                    {
                        _log.Warning(
                            new InvalidOperationException("An earlier synchronization did not complete."),
                            $"Recovering from interrupted synchronization started at {previousRun.StartedAtUtc:O}. " +
                            "Reconciliation will be replayed from the current ready source.");
                    }
                }

                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        new OperationImpl()
                            .InitializeOperation(
                                FolderOptions.SourceFolder,
                                FolderOptions.DestinationFolder,
                                FolderOptions.DryRun,
                                FolderOptions.AllowEmptySource,
                                FolderOptions.SourceReadyFile)
                            .Update()
                            .Copy()
                            .Delete();

                        _log.Info("Synchronization job completed.");
                        journal?.Complete();
                        SynchronizationMetrics.RecordJobCompleted();
                        return;
                    }
                    catch (IOException exception) when (
                        attempt < maxAttempts && RetryPolicy.IsTransient(exception))
                    {
                        var retryDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                        _log.Warning(exception, $"Transient I/O failure; retrying in {retryDelay.TotalSeconds:0} second(s) (attempt {attempt}/{maxAttempts}).");
                        await Task.Delay(retryDelay, context.CancellationToken);
                    }
                    catch (Exception exception)
                    {
                        SynchronizationMetrics.RecordJobFailed();
                        _log.Error(
                            exception,
                            journal is null
                                ? "Synchronization dry-run failed."
                                : $"Synchronization failed. Recovery journal retained at {FolderOptions.LogFilePath}.");
                        throw new JobExecutionException("Synchronization job failed.", exception);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _log.Warning(
                    new OperationCanceledException("Synchronization was cancelled; the recovery journal was retained."),
                    "Synchronization cancelled before completion.");
                throw;
            }
            catch (Exception exception) when (exception is not JobExecutionException)
            {
                SynchronizationMetrics.RecordJobFailed();
                _log.Error(
                    exception,
                    journal is null
                        ? "Synchronization dry-run failed."
                        : $"Synchronization could not start or recover. Recovery journal retained at {FolderOptions.LogFilePath}.");
                throw new JobExecutionException("Synchronization could not start or recover.", exception);
            }
            finally
            {
                stopwatch.Stop();
                SynchronizationMetrics.RecordJobDuration(stopwatch.Elapsed);
            }
        }
    }
}
