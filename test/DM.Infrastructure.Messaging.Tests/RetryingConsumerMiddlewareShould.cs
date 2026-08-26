using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using DM.Infrastructure.Core.Tracing;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// Both workers consume through one middleware, and a message it was thrown at
/// reaches its consumer a second time.
/// </summary>
/// <remarks>
/// Here rather than in the test project of a worker: the middleware belongs to the
/// broker side, and after the two copies were folded into one it belongs to no single
/// host at all. What is left in a worker - the letter its processor builds - is tested
/// next to that processor.
/// </remarks>
public class RetryingConsumerMiddlewareShould
{
    /// <summary>
    /// A queue of this test alone. The instruments are static fields of the process
    /// and the classes of this assembly run in parallel, so the label is what tells
    /// these measurements from anyone else's.
    /// </summary>
    private const string Queue = "dm.test.retrying";

    private static readonly KeyValuePair<string, object?> QueueLabel = MessagingMetrics.Queue(Queue);

    [Fact]
    public void BeResolvableByItsTypeAlone()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddDmRetryingConsumer(Queue)
            .BuildServiceProvider();

        provider.GetRequiredService<RetryingConsumerMiddleware>().Should().NotBeNull(
            "the client resolves an interface middleware out of the container by its type and " +
            "hands it nothing, so a queue that is not in the graph is a middleware the pipeline " +
            "cannot build and a consumer that stops on its first message");
    }

    /// <summary>
    /// Two seconds of real time, and they are the first wait of the policy the hosts
    /// actually run under.
    /// </summary>
    /// <remarks>
    /// What they buy is the assertion the middleware exists for: not that a retry was
    /// decided - which the log says before the wait begins - but that the message was
    /// handed to the consumer again afterwards, counted under its queue, and answered
    /// with the result of the attempt that worked.
    ///
    /// The exhausted branch is deliberately not here. It is the whole ladder, 62
    /// seconds, and reaching it needs either the number of attempts in the test's hands
    /// or the clock of the process in them - the first moves the policy the production
    /// hosts run under into a test parameter, the second is global state in a suite that
    /// runs its classes in parallel.
    /// </remarks>
    [Fact]
    public async Task HandAMessageItsConsumerThrewOnBackToIt()
    {
        var logger = new RecordingLogger<RetryingConsumerMiddleware>();
        var counted = 0L;
        using var listener = Listening(measurement => counted += measurement);

        var attempts = 0;
        var result = await new RetryingConsumerMiddleware(Queue, logger).InvokeAsync(
            // The pipeline hands the context to the next step and reads nothing out of
            // it. The client builds one inside its own consume loop, out of constructors
            // it keeps internal, and fabricating one here through reflection would
            // assert nothing this does not.
            context: null!,
            next: (_, _) =>
            {
                attempts++;
                return attempts == 1
                    ? throw new InvalidOperationException("the relay went away")
                    : Task.FromResult(ProcessResult.Success);
            },
            cancellationToken: CancellationToken.None);

        attempts.Should().Be(2,
            "a message the consumer threw on is worth another attempt, and that is the whole " +
            "of what stands between a relay that blinked and a letter in the dead-letter queue");
        result.Should().Be(ProcessResult.Success,
            "the attempt that succeeded is the one the client is answered with");
        counted.Should().Be(1,
            "a retry nobody counts leaves a queue on its way out looking exactly like a quiet " +
            "one, with a warning line as the only trace");
        logger.At(LogLevel.Warning).Should().ContainSingle()
            .Which.Should().Contain(Queue,
                "the warning is read beside the other queues, and one that names none belongs " +
                "to any of them");
    }

    /// <summary>
    /// Listens to the retry counter of this queue, and of no other.
    /// </summary>
    /// <param name="measured">Called with every retry counted for this queue.</param>
    private static MeterListener Listening(Action<long> measured)
    {
        // Both names are read here, before the listener exists, and the callback
        // below closes over the strings rather than over the fields.
        //
        // Publishing an instrument calls InstrumentPublished from inside the
        // constructor of that instrument, and the instruments are static fields
        // initialised in order. A callback that reads MessagingMetrics.Retried
        // therefore runs while the first counter of that class is still being
        // built: the field it wants is null, and the whole type initializer dies
        // with a NullReferenceException naming nothing that explains it.
        var meter = MessagingMetrics.MeterName;
        var retried = MessagingMetrics.Retried.Name;

        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, active) =>
            {
                if (instrument.Meter.Name == meter && instrument.Name == retried)
                {
                    active.EnableMeasurementEvents(instrument);
                }
            }
        };

        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == QueueLabel.Key && Equals(tag.Value, QueueLabel.Value))
                {
                    measured(measurement);
                }
            }
        });

        listener.Start();
        return listener;
    }
}
