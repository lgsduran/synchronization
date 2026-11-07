using Quartz;
using Quartz.Impl;
using Synchronization.Job;
using Synchronization.Logs;
using Synchronization.Settings;

class Program
{
    private static ConsoleLog _log = new ConsoleLog("Main");
    static async Task Main(string[] args)
    {
        IScheduler? scheduler = null;
        using var cancellationTokenSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            SettingOptions.GetSettings();

            var schedulerFactory = new StdSchedulerFactory();
            scheduler = await schedulerFactory.GetScheduler();
            await scheduler.Start();

            var job = JobBuilder.Create<SynchronizationJob>()
                .WithIdentity("SynchronizationJob", "SynchronizationGroup")
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity("SynchronizationTrigger", "SynchronizationGroup")
                .StartNow()
                .WithSimpleSchedule(x => x
                    .WithInterval(TimeSpan.FromSeconds(FolderOptions.IntervalSeconds))
                    .RepeatForever())
                .Build();

            await scheduler.ScheduleJob(job, trigger);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            _log.Info("Shutdown requested.");
        }
        catch (Exception exception)
        {
            _log.Error(exception, "Application failed.");
            throw;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
            if (scheduler is not null)
                await scheduler.Shutdown(waitForJobsToComplete: true);

            _log.Info("Scheduler has been shut down.");
        }
    }   
}