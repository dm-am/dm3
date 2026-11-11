using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Infrastructure.Messaging;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Core.Queues;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging.Queues;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace DM.Workers.Mail.Tests;

/// <summary>
/// What the mail worker makes of a claimed row: the letter the mail sender stored goes to the
/// unchanged letter processor, and text that is not a letter is a rejection.
/// </summary>
public class MailQueueDecodingShould
{
    /// <remarks>Mutation it must fail on: a throw on unreadable text, which spends the whole ladder on it.</remarks>
    [Fact]
    public async Task RejectARowWhoseTextIsNotALetter()
    {
        var processor = Substitute.For<IProcessor<string, EmailLetter>>();

        var result = await MailQueue.Handle(Row("{\"address\": 42"), Scope(processor), CancellationToken.None);

        result.Should().Be(ProcessResult.Failure, "no retry turns this text into a letter");
        await processor.DidNotReceiveWithAnyArgs().Process(default!, default!, default);
    }

    [Fact]
    public async Task RejectARowWithoutText()
    {
        var processor = Substitute.For<IProcessor<string, EmailLetter>>();

        var result = await MailQueue.Handle(Row(null), Scope(processor), CancellationToken.None);

        result.Should().Be(ProcessResult.Failure);
    }

    [Fact]
    public async Task HandTheStoredLetterWithAZeroCharacterToTheProcessorUnderTheQueueName()
    {
        var processor = Substitute.For<IProcessor<string, EmailLetter>>();
        EmailLetter? received = null;
        string? key = null;
        processor.Process(Arg.Do<string>(k => key = k), Arg.Do<EmailLetter>(l => received = l), Arg.Any<CancellationToken>())
            .Returns(ProcessResult.Success);
        var stored = LetterPayload.Write(new EmailLetter
        {
            Address = "player@example.com",
            Subject = "New post in Game\0of Ages",
            Body = "<p>A title with a zero \0 character</p>",
            LinkedResources = [new LinkedResource { ContentId = "logo", MimeType = "image/png", Content = [1, 0, 2] }],
        });

        var result = await MailQueue.Handle(Row(stored), Scope(processor), CancellationToken.None);

        result.Should().Be(ProcessResult.Success);
        key.Should().Be(QueueNames.Mail);
        received!.Address.Should().Be("player@example.com");
        received.Subject.Should().Be("New post in Game\0of Ages");
        received.Body.Should().Be("<p>A title with a zero \0 character</p>");
        received.LinkedResources.Should().ContainSingle().Which.Content.Should().Equal(1, 0, 2);
    }

    private static OutboxRow Row(string? payload) => new(1, Guid.NewGuid(), null, null, payload, null);

    private static IServiceProvider Scope(IProcessor<string, EmailLetter> processor) =>
        new ServiceCollection().AddSingleton(processor).BuildServiceProvider();
}
