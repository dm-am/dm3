using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.HostedServices;

/// <summary>
/// A job the host runs on a fixed interval for as long as it lives.
/// </summary>
/// <remarks>
/// The loop, the wait, the cancellation and the scope live here once; a job
/// supplies only the pass.
/// </remarks>
/// <param name="serviceProvider">Root provider, from which each pass gets its own scope.</param>
/// <param name="logger">Logger of the concrete job, so its lines keep its category.</param>
internal abstract class PeriodicHostedService(IServiceProvider serviceProvider, ILogger logger) : BackgroundService
{
    protected ILogger Logger { get; } = logger;

    /// <summary>Prefix every line of this job carries, e.g. "[Token Cleanup]".</summary>
    protected abstract string Tag { get; }

    /// <summary>Time between two passes.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>Wait before the first pass, so the rest of the host can settle.</summary>
    protected virtual TimeSpan StartupDelay => TimeSpan.Zero;

    /// <summary>
    /// One pass over the job's work.
    /// </summary>
    /// <param name="scope">Provider of the scope opened for this pass.</param>
    /// <param name="cancellationToken">Stopping token of the host.</param>
    protected abstract Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Async from the first statement: a cancellation inside host startup stops
    /// the host.
    /// </remarks>
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        // A TimeSpan, not TotalHours: an interval may be shorter than an hour.
        Logger.LogInformation(
            "{Tag} Service started. Every {Interval}, first pass after {StartupDelay}",
            Tag, Interval, StartupDelay);

        using var timer = new PeriodicTimer(Interval);

        try
        {
            if (StartupDelay > TimeSpan.Zero)
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await Guarded("the periodic pass", async () =>
                {
                    using var scope = serviceProvider.CreateScope();
                    await RunOnce(scope.ServiceProvider, stoppingToken);
                });
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("{Tag} Service is stopping", Tag);
        }
    }

    /// <summary>
    /// Runs a step under its own guard: a failure is logged and the next pass is
    /// the retry, so it costs neither the job nor the steps after it.
    /// </summary>
    protected async Task Guarded(string step, Func<Task> run)
    {
        try
        {
            await run();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            Logger.LogError(e, "{Tag} Unexpected error in {Step}", Tag, step);
        }
    }
}
