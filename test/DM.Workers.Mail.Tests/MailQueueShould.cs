using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Core.Tracing;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.Queues;
using DM.Infrastructure.Persistence.Entities.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Xunit;

namespace DM.Workers.Mail.Tests;

/// <summary>
/// A letter travels through the table: the mail sender stores it as a row of the mail queue, the
/// mail worker's loop claims it and hands it to the letter processor, and the row comes out
/// processed with its text erased, retried or dead.
/// </summary>
/// <remarks>
/// Letters go in through the mail sender, the proof is the state of the row and the letters the
/// processor received. The processor stands in for the mail server: a throw is a refusal. Each test
/// starts from an empty mail queue, since the sender has only the one.
/// </remarks>
[Collection(MailDatabaseCollection.Name)]
public class MailQueueShould(MailDatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        await using var context = fixture.CreateContext();
        await context.OutboxEvents.Where(row => row.Queue == QueueNames.Mail).ExecuteDeleteAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <remarks>Mutation it must fail on: marking the row processed without erasing its text and last error.</remarks>
    [Fact]
    public async Task MarkASentLetterAndEraseItsTextAndLastErrorInTheSameCommand()
    {
        var clock = new MovingClock(Start);
        await using var services = Services(clock, out var server);
        server.RefuseTimes(1);
        await Send(services, Letter("erased@example.com"));
        var worker = Worker(services, clock);

        (await worker.HandleNextAsync(CancellationToken.None)).Should().BeTrue();
        var refused = await SingleRow();
        refused.LastError.Should().NotBeNull("the first attempt was refused");
        refused.Payload.Should().NotBeNull("a letter waiting for its next attempt keeps its text");

        clock.Now = refused.NextAttemptUtc!.Value;
        (await worker.HandleNextAsync(CancellationToken.None)).Should().BeTrue();

        var sent = await SingleRow();
        sent.ProcessedUtc.Should().Be(clock.Now);
        sent.Payload.Should().BeNull("the body of a sent letter, with its live link, leaves the table at once");
        sent.LastError.Should().BeNull("a sent letter carries no error");
        server.Received.Should().ContainSingle().Which.Address.Should().Be("erased@example.com");
    }

    /// <remarks>Mutation it must fail on: the body stored as text, which PostgreSQL refuses with a zero character.</remarks>
    [Fact]
    public async Task StoreAndSendALetterWithAZeroCharacterInItsBody()
    {
        var clock = new MovingClock(Start);
        await using var services = Services(clock, out var server);
        var letter = Letter("zero@example.com", body: "<p>Game \0 of the night</p>");

        await Send(services, letter);
        (await Worker(services, clock).HandleNextAsync(CancellationToken.None)).Should().BeTrue();

        server.Received.Should().ContainSingle().Which.Body.Should().Be("<p>Game \0 of the night</p>",
            "one odd character typed by another user must not swallow the recipient's mail");
        (await SingleRow()).ProcessedUtc.Should().Be(Start);
    }

    /// <remarks>Mutation it must fail on: unreadable text thrown as a failure, which climbs the ladder.</remarks>
    [Fact]
    public async Task KillARowThatIsNotALetterOnTheFirstPassAndSendTheRowBehindInTheSamePass()
    {
        var clock = new MovingClock(Start);
        await using var services = Services(clock, out var server);
        var unreadable = await StoreRaw("{\"address\":");
        clock.Now = Start.AddSeconds(1);
        await Send(services, Letter("behind@example.com"));
        var passEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = Worker(services, clock, (_, stoppingToken) =>
        {
            passEnded.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        });

        await worker.StartAsync(CancellationToken.None);
        await passEnded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None);

        var dead = await Read(unreadable.Id);
        dead.DeadUtc.Should().NotBeNull("no retry turns this text into a letter");
        dead.Attempts.Should().Be(0);
        server.Received.Should().ContainSingle("the row behind is sent in the same pass")
            .Which.Address.Should().Be("behind@example.com");
    }

    /// <remarks>
    /// Mutations it must fail on: waiting for the server in memory, a refusal that makes the row
    /// dead, a pause that is not taken from the row.
    /// </remarks>
    [Fact]
    public async Task SendTheLetterOnTheFirstAttemptAfterTheServerIsBack()
    {
        var clock = new MovingClock(Start);
        await using var services = Services(clock, out var server);
        server.RefuseTimes(4);
        await Send(services, Letter("patient@example.com"));
        var worker = Worker(services, clock);

        for (var refusal = 1; refusal <= 4; refusal++)
        {
            (await worker.HandleNextAsync(CancellationToken.None)).Should().BeTrue();
            var waiting = await SingleRow();
            waiting.Attempts.Should().Be(refusal);
            waiting.DeadUtc.Should().BeNull("a short outage costs only a delay");

            clock.Now = waiting.NextAttemptUtc!.Value - TimeSpan.FromMilliseconds(1);
            (await worker.HandleNextAsync(CancellationToken.None)).Should().BeFalse("the pause is not over yet");
            clock.Now = waiting.NextAttemptUtc!.Value;
        }

        (await worker.HandleNextAsync(CancellationToken.None)).Should().BeTrue();

        server.Attempts.Should().Be(5, "four refusals and the one attempt after the server came back");
        server.Received.Should().ContainSingle();
        (await SingleRow()).ProcessedUtc.Should().Be(clock.Now);
    }

    /// <remarks>Mutation it must fail on: a span started without the parent stored on the row.</remarks>
    [Fact]
    public async Task ContinueTheTraceOfTheRequestThatStoredTheLetter()
    {
        var clock = new MovingClock(Start);
        await using var services = Services(clock, out _);
        var spans = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DmActivitySource.Source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Add,
        };
        ActivitySource.AddActivityListener(listener);

        ActivityTraceId requestTrace;
        using (var request = new Activity("POST /v1/account/register").SetIdFormat(ActivityIdFormat.W3C).Start())
        {
            requestTrace = request.TraceId;
            await Send(services, Letter("traced@example.com"));
        }

        (await Worker(services, clock).HandleNextAsync(CancellationToken.None)).Should().BeTrue();

        spans.Should().Contain(span => span.DisplayName == $"{QueueNames.Mail} process")
            .Which.TraceId.Should().Be(requestTrace,
                "a registration can be followed from the form to the mail server");
    }

    private static EmailLetter Letter(string address, string body = "<p>Confirm</p>") => new()
    {
        Address = address,
        Subject = "Confirm your registration",
        Body = body,
    };

    private ServiceProvider Services(IDateTimeProvider clock, out RefusingServer server)
    {
        var refusing = new RefusingServer();
        server = refusing;
        var processor = Substitute.For<IProcessor<string, EmailLetter>>();
        processor.Process(Arg.Any<string>(), Arg.Any<EmailLetter>(), Arg.Any<CancellationToken>())
            .Returns(call => refusing.Take(call.ArgAt<EmailLetter>(1)));

        var guids = Substitute.For<IGuidFactory>();
        guids.Create().Returns(_ => Guid.NewGuid());

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(NpgsqlDataSource.Create(fixture.ConnectionString));
        services.AddSingleton(clock);
        services.AddSingleton(guids);
        services.AddSingleton(processor);
        services.AddDmMail();
        return services.BuildServiceProvider();
    }

    private static async Task Send(IServiceProvider services, EmailLetter letter)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMailSender>().SendAsync(letter);
    }

    private static QueueWorker Worker(
        IServiceProvider services, IDateTimeProvider clock, Func<TimeSpan, CancellationToken, Task>? wait = null) => new(
        QueueNames.Mail,
        null,
        MailQueue.Handle,
        services.GetRequiredService<NpgsqlDataSource>(),
        services.GetRequiredService<IServiceScopeFactory>(),
        clock,
        NullLogger<QueueWorker>.Instance,
        wait);

    private async Task<OutboxEvent> StoreRaw(string payload)
    {
        await using var context = fixture.CreateContext();
        var row = new OutboxEvent
        {
            EventId = Guid.NewGuid(),
            Queue = QueueNames.Mail,
            Payload = payload,
            OccurredUtc = Start,
        };
        context.OutboxEvents.Add(row);
        await context.SaveChangesAsync();
        return row;
    }

    private async Task<OutboxEvent> SingleRow()
    {
        await using var context = fixture.CreateContext();
        return await context.OutboxEvents.AsNoTracking().SingleAsync(row => row.Queue == QueueNames.Mail);
    }

    private async Task<OutboxEvent> Read(long id)
    {
        await using var context = fixture.CreateContext();
        return await context.OutboxEvents.AsNoTracking().SingleAsync(row => row.Id == id);
    }

    private sealed class MovingClock(DateTimeOffset now) : IDateTimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
    }

    /// <summary>A mail server that refuses a given number of attempts and then accepts.</summary>
    private sealed class RefusingServer
    {
        private int _refusalsLeft;

        public List<EmailLetter> Received { get; } = [];

        public int Attempts { get; private set; }

        public void RefuseTimes(int times) => _refusalsLeft = times;

        public ProcessResult Take(EmailLetter letter)
        {
            Attempts++;
            if (_refusalsLeft > 0)
            {
                _refusalsLeft--;
                throw new IOException("Connection refused by the mail server");
            }

            Received.Add(letter);
            return ProcessResult.Success;
        }
    }
}
