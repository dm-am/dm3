using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Infrastructure.Messaging.Queues;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A row of the notification queue that holds no event cannot be handled by any retry, so the
/// worker's handler answers that it cannot be processed and the loop makes it dead at once.
/// </summary>
public class NotificationQueueRejectionShould
{
    [Theory]
    [InlineData((int)EventType.NewTopic, false)]
    [InlineData(null, true)]
    public async Task AnswerThatARowWithoutItsEventCannotBeProcessed(int? eventType, bool hasEntity)
    {
        var processor = Substitute.For<IProcessor<string, InvokedEvent>>();
        await using var services = new ServiceCollection().AddSingleton(processor).BuildServiceProvider();
        var row = new OutboxRow(1, Guid.NewGuid(), eventType, hasEntity ? Guid.NewGuid() : null, null, null);

        var result = await NotificationQueue.Handle(row, services, CancellationToken.None);

        result.Should().Be(ProcessResult.Failure, "a row that cannot be decoded is a rejection, not a failure to retry");
        await processor.DidNotReceiveWithAnyArgs().Process(default!, default!, default);
    }
}
