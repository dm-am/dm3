using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Infrastructure.Messaging.Queues;
using DM.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// A worker loop that cannot reach its database waits on a ladder of its own and lives on.
/// </summary>
/// <remarks>No database at all: the source points at a closed port, so every pass is a loop error.</remarks>
public class QueueLoopErrorsShould
{
    /// <remarks>Mutation it must fail on: no catch around a pass, which faults the loop and stops the host.</remarks>
    [Fact]
    public async Task WaitFromOneSecondDoublingUpToAMinuteWithoutStopping()
    {
        const int observed = 7;
        var pauses = new ConcurrentQueue<TimeSpan>();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var closedPort = NpgsqlDataSource.Create(
            "Host=127.0.0.1;Port=1;Username=nobody;Password=nothing;Database=none;Timeout=1;Pooling=false");
        await using var services = new ServiceCollection().BuildServiceProvider();
        var worker = new QueueWorker(
            "test.closed-port",
            null,
            (_, _, _) => Task.FromResult(ProcessResult.Success),
            closedPort,
            services.GetRequiredService<IServiceScopeFactory>(),
            new FixedClock(DateTimeOffset.UnixEpoch),
            NullLogger<QueueWorker>.Instance,
            (pause, stoppingToken) =>
            {
                pauses.Enqueue(pause);
                if (pauses.Count < observed)
                {
                    return Task.CompletedTask;
                }

                reached.TrySetResult();
                return Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            });

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await Task.WhenAny(reached.Task, worker.ExecuteTask!).WaitAsync(TimeSpan.FromSeconds(60));
            worker.ExecuteTask!.IsCompleted.Should().BeFalse("a loop error never ends the loop, or the host stops");
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        pauses.Take(observed).Select(pause => pause.TotalSeconds).Should().Equal(
            [1, 2, 4, 8, 16, 32, 60],
            "the loop waits from one second, doubling, up to sixty");
        worker.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue("a stop ends the loop quietly");
    }
}
