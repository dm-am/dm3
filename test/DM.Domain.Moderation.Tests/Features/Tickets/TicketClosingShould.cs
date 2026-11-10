using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Tickets;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Tickets;

public class TicketClosingShould : TicketServiceTestBase
{
    /// <summary>
    /// An open ticket of the given reporter, as the repository reads it, and a
    /// closing without a word that goes through.
    /// </summary>
    private void TicketAnsweredBy(Guid? reporterUserId)
    {
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = ticketId,
            ReporterUserId = reporterUserId,
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.UserComplaint
        });
        ClosingSucceeds();
    }

    [Theory]
    [InlineData(null, TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.WaitingForUser, TicketStatus.WaitingForUser)]
    [InlineData(TicketStatus.Closed, TicketStatus.Closed)]
    [InlineData(TicketStatus.Spam, TicketStatus.Spam)]
    public async Task WriteTheAnswerOfModerationWithTheStatusItChose(TicketStatus? chosen, TicketStatus expected)
    {
        TicketAnsweredBy(Guid.NewGuid());
        var written = CommentsTaken(out var statusAfter);

        await service.CreateComment(ticketId, new CreateTicketComment { Text = "Разобрались", Status = chosen });

        statusAfter().Should().Be(expected);
        written().AuthorId.Should().Be(currentUserId);
        written().Text.Should().Be("Разобрались");
        await ticketRepository.DidNotReceiveWithAnyArgs().Close(default, default, default, default, default);
    }

    [Theory]
    [InlineData(TicketStatus.Closed, "")]
    [InlineData(TicketStatus.Spam, "   ")]
    public async Task CloseWithoutAWordWhenTheAnswerHasNone(TicketStatus status, string text)
    {
        TicketAnsweredBy(Guid.NewGuid());

        await service.CreateComment(ticketId, new CreateTicketComment { Text = text, Status = status });

        await ticketRepository.Received(1).Close(ticketId, status, currentUserId, now, Arg.Any<CancellationToken>());
        await ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    [Fact]
    public async Task ReadTheTicketBackAfterTheAnswer()
    {
        var reporter = Guid.NewGuid();
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns(
            new Ticket
            {
                TicketId = ticketId,
                ReporterUserId = reporter,
                Status = TicketStatus.WaitingForModeration,
                Subtype = TicketSubtype.UserComplaint
            },
            new Ticket
            {
                TicketId = ticketId,
                ReporterUserId = reporter,
                Status = TicketStatus.Closed,
                Subtype = TicketSubtype.UserComplaint,
                ResolvedByUsername = "CurrentUser"
            });
        ClosingSucceeds();

        var page = await service.CreateComment(ticketId, new CreateTicketComment { Status = TicketStatus.Closed });

        page.TicketId.Should().Be(ticketId);
        page.Status.Should().Be(TicketStatus.Closed);
        page.ResolvedByUsername.Should().Be("CurrentUser");
        page.CanComment.Should().BeFalse();
        page.CanResolve.Should().BeFalse();
    }

    /// <summary>
    /// The author of a closed ticket learns of the closing from a notification,
    /// once: an answer that closes the ticket is the closing, and its last word
    /// stands on the page the notification leads to.
    /// </summary>
    [Theory]
    [InlineData("Предупреждение выдано, спасибо")]
    [InlineData("")]
    public async Task AnnounceTheClosingToTheAuthorOfTheTicket(string text)
    {
        TicketAnsweredBy(Guid.NewGuid());
        CommentsTaken();

        await service.CreateComment(ticketId, new CreateTicketComment { Text = text, Status = TicketStatus.Closed });

        await eventProducer.Received(1).SendAsync(EventType.TicketResolved, ticketId);
        await eventProducer.DidNotReceive().SendAsync(EventType.TicketAnswered, Arg.Any<Guid>());
    }

    // A guest has no inbox: his tracking page is where the answer appears.
    [Theory]
    [InlineData("Доступ восстановлен")]
    [InlineData("")]
    public async Task AnnounceNoClosingToAGuest(string text)
    {
        TicketAnsweredBy(null);
        CommentsTaken();

        await service.CreateComment(ticketId, new CreateTicketComment { Text = text, Status = TicketStatus.Closed });

        await eventProducer.DidNotReceiveWithAnyArgs().SendAsync(default(EventType), default(Guid));
    }

    [Theory]
    [InlineData("Реклама")]
    [InlineData("")]
    public async Task AnnounceNothingAboutSpam(string text)
    {
        TicketAnsweredBy(Guid.NewGuid());
        CommentsTaken();

        await service.CreateComment(ticketId, new CreateTicketComment { Text = text, Status = TicketStatus.Spam });

        await eventProducer.DidNotReceiveWithAnyArgs().SendAsync(default(EventType), default(Guid));
    }

    [Fact]
    public async Task RefuseAClosingThatLostTheRace()
    {
        TicketAnsweredBy(Guid.NewGuid());
        ticketRepository.Close(Arg.Any<Guid>(), Arg.Any<TicketStatus>(), Arg.Any<Guid>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var act = () => service.CreateComment(ticketId, new CreateTicketComment { Status = TicketStatus.Closed });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest && e.Message.Contains("уже закрыто"));
        await eventProducer.DidNotReceiveWithAnyArgs().SendAsync(default(EventType), default(Guid));
    }

    [Theory]
    [InlineData("", "уже закрыто")]
    [InlineData("И еще одно", "Обращение закрыто")]
    public async Task RefuseToCloseAClosedTicket(string text, string refusal)
    {
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = ticketId,
            ReporterUserId = Guid.NewGuid(),
            Status = TicketStatus.Closed,
            Subtype = TicketSubtype.UserComplaint
        });

        var act = () => service.CreateComment(ticketId,
            new CreateTicketComment { Text = text, Status = TicketStatus.Spam });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest && e.Message.Contains(refusal));
        await ticketRepository.DidNotReceiveWithAnyArgs().Close(default, default, default, default, default);
        await ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }
}
