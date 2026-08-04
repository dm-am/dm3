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
/// Nine services carried a copy of the same loop - a PeriodicTimer, a while over
/// the stopping token, a catch for cancellation, a catch for everything else and
/// a scope per pass - and the copies had drifted. Six ran their first pass
/// outside the guard, one ran it inside, one had none; two waited before the
/// first pass and seven did not; one duplicated the tick wait inside its own
/// error branch. Work done before the first await is work done inside
/// ExecuteAsync's synchronous prologue, and a cancellation raised there leaves
/// ExecuteAsync, which BackgroundService reports as a critical failure and
/// answers by stopping the host: every deployment rolled back within its first
/// minute produced one.
///
/// What is left to a job is the pass. The loop, the wait, the cancellation and
/// the scope are here, once, so a change to any of them is one edit rather than
/// nine.
/// </remarks>
internal abstract class PeriodicHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Creates the job.
    /// </summary>
    /// <param name="serviceProvider">Root provider, from which each pass gets its own scope.</param>
    /// <param name="logger">Logger of the concrete job, so its lines keep its category.</param>
    protected PeriodicHostedService(IServiceProvider serviceProvider, ILogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>Prefix every line of this job carries, e.g. "[Token Cleanup]".</summary>
    protected abstract string Tag { get; }

    /// <summary>Time between two passes.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>
    /// Wait before the first pass. Zero unless the job has a reason to let the
    /// rest of the host settle first.
    /// </summary>
    protected virtual TimeSpan StartupDelay => TimeSpan.Zero;

    /// <summary>
    /// One pass over the job's work.
    /// </summary>
    /// <param name="scope">Provider of the scope opened for this pass.</param>
    /// <param name="cancellationToken">Stopping token of the host.</param>
    protected abstract Task RunOnce(IServiceProvider scope, CancellationToken cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Sealed: the whole point of this type is that there is one loop. Async from
    /// the first statement, so nothing here runs inside host startup.
    /// </remarks>
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        _logger.LogInformation(
            "{Tag} Service started. Every {IntervalHours}h, first pass after {DelayMinutes}m",
            Tag, Interval.TotalHours, StartupDelay.TotalMinutes);

        using var timer = new PeriodicTimer(Interval);

        try
        {
            if (StartupDelay > TimeSpan.Zero)
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await Pass(stoppingToken);
                await timer.WaitForNextTickAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("{Tag} Service is stopping", Tag);
        }
    }

    /// <summary>
    /// One guarded pass. A failing pass is logged and the job waits out the
    /// interval rather than dying: the next pass is the retry.
    /// </summary>
    private async Task Pass(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            await RunOnce(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // The host is stopping. Handled by the loop, which says so once.
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{Tag} Unexpected error in the periodic pass", Tag);
        }
    }
}
