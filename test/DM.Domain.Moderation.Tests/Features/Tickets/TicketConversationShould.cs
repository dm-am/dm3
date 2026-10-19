using System;
using System.Linq;
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

/// <summary>
/// The conversation of a ticket: whose turn a comment passes to, how it is
/// stored, who may write it and with what, and where it stops.
/// </summary>
public class TicketConversationShould : TicketServiceTestBase
{
    /// <summary>
    /// A comment of a moderator is stored as typed, [mod] in whatever case he
    /// wrote it (BBCODE_RENDERING.md, "Регистр имен").
    /// </summary>
    [Fact]
    public async Task StoreTheCommentAsTyped()
    {
        var captured = OpenTicketTakingComments(Guid.NewGuid());

        await service.CreateComment(ticketId, new CreateTicketComment { Text = "[MOD]Разобрались[/MOD]" });

        captured().Text.Should().Be("[MOD]Разобрались[/MOD]");
    }

    /// <summary>
    /// The author's comment leaves the ticket waiting for moderation, a moderator's
    /// waiting for the author: the state says whose move it is.
    /// </summary>
    [Theory]
    [InlineData(true, TicketStatus.WaitingForModeration)]
    [InlineData(false, TicketStatus.WaitingForUser)]
    public async Task PassTheTurnWithAComment(bool byAuthor, TicketStatus expected)
    {
        SetCurrentUser(byAuthor ? UserRole.RegularUser : UserRole.Moderator);
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = ticketId,
            ReporterUserId = byAuthor ? currentUserId : Guid.NewGuid(),
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.UserComplaint
        });
        var written = CommentsTaken(out var statusAfter);

        await service.CreateComment(ticketId, new CreateTicketComment { Text = "Ответ" });

        statusAfter().Should().Be(expected);
        written().AuthorId.Should().Be(currentUserId);
        written().TicketId.Should().Be(ticketId);
    }

    /// <summary>
    /// A moderator who filed a ticket is its side: his comment is a reply of the
    /// author, not an answer of moderation.
    /// </summary>
    [Fact]
    public async Task TakeTheCommentOfAModeratorWhoFiledTheTicketAsAReply()
    {
        SetCurrentUser(UserRole.SeniorModerator);
        OpenTicketTakingComments(currentUserId);
        CommentsTaken(out var statusAfter);

        await service.CreateComment(ticketId, new CreateTicketComment { Text = "Дополню" });

        statusAfter().Should().Be(TicketStatus.WaitingForModeration);
        await eventProducer.Received(1).SendAsync(EventType.TicketReplied, commentId);
        await eventProducer.DidNotReceive().SendAsync(EventType.TicketAnswered, Arg.Any<Guid>());
    }

    /// <summary>
    /// The guest writes by the token, signed by nobody, and his comment waits for
    /// moderation; whoever holds the token, the text loses [mod] as a guest's does.
    /// </summary>
    [Fact]
    public async Task WriteTheCommentOfTheTokenAsTheGuests()
    {
        SetCurrentUser(UserRole.Admin);
        ticketRepository.GetByTrackingToken("tok123", Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = ticketId,
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.AccessRecovery,
            IsGuest = true
        });
        var written = CommentsTaken(out var statusAfter);

        await service.CreateTrackedComment("tok123",
            new CreateTicketComment { Text = "Вот [mod]для модерации[/mod] почта" });

        written().AuthorId.Should().BeNull("the comment of the token is the guest's, whoever is signed in");
        written().Text.Should().Be("Вот для модерации почта",
            "the block of a guest is unwrapped, as every guest text is");
        statusAfter().Should().Be(TicketStatus.WaitingForModeration);
        await eventProducer.Received(1).SendAsync(EventType.TicketReplied, commentId);
    }

    [Fact]
    public async Task AnswerAnUnknownTokenWithNotFound()
    {
        ticketRepository.GetByTrackingToken(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Ticket?)null);

        var act = () => service.CreateTrackedComment("nope", new CreateTicketComment { Text = "Есть кто?" });

        await act.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    /// <summary>
    /// [mod] stays in the comment of a moderator, and the comment of a user loses
    /// the block and keeps its words, as in every comment of the site.
    /// </summary>
    [Theory]
    [InlineData(UserRole.RegularUser, "Текст служебное")]
    [InlineData(UserRole.Moderator, "Текст [mod]служебное[/mod]")]
    public async Task KeepTheModBlockOfAModeratorOnly(UserRole role, string stored)
    {
        SetCurrentUser(role);
        var written = OpenTicketTakingComments(role == UserRole.RegularUser ? currentUserId : Guid.NewGuid());

        await service.CreateComment(ticketId, new CreateTicketComment { Text = "Текст [mod]служебное[/mod]" });

        written().Text.Should().Be(stored);
    }

    /// <summary>
    /// A ban that silences discussions does not silence the author in his own
    /// ticket: the ticket is how a sanctioned user reaches moderation.
    /// </summary>
    [Fact]
    public async Task LetTheAuthorUnderADemocraticBanWrite()
    {
        SetCurrentUser(UserRole.RegularUser, AccessPolicy.DemocraticBan);
        var written = OpenTicketTakingComments(currentUserId);

        await service.CreateComment(ticketId, new CreateTicketComment { Text = "Прошу пересмотреть" });

        written().AuthorId.Should().Be(currentUserId);
    }

    /// <summary>
    /// The author of a ticket chooses no status, even when his role would let him:
    /// a moderator who filed a complaint does not close it as spam in the name of
    /// moderation. The answer that asks for one is refused whole, text included,
    /// rather than written without the status it asked for.
    /// </summary>
    [Theory]
    [InlineData(UserRole.SeniorModerator, TicketStatus.Spam, "")]
    [InlineData(UserRole.SeniorModerator, TicketStatus.Closed, "Закрываю")]
    [InlineData(UserRole.RegularUser, TicketStatus.Closed, "Вопрос решен")]
    [InlineData(UserRole.RegularUser, TicketStatus.WaitingForUser, "Жду ответа")]
    public async Task RefuseAStatusFromTheAuthor(UserRole role, TicketStatus status, string text)
    {
        SetCurrentUser(role);
        OpenTicketTakingComments(currentUserId);
        ClosingSucceeds();

        var page = await service.GetTicket(ticketId);
        var answer = () => service.CreateComment(ticketId, new CreateTicketComment { Text = text, Status = status });

        page.CanResolve.Should().BeFalse();
        page.CanComment.Should().BeTrue();
        await answer.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await ticketRepository.DidNotReceiveWithAnyArgs().Close(default, default, default, default, default);
        await ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    /// <summary>
    /// The holder of the token answers as the guest who filed the ticket, and the
    /// author chooses no status.
    /// </summary>
    [Fact]
    public async Task RefuseAStatusFromTheHolderOfTheToken()
    {
        ticketRepository.GetByTrackingToken("tok123", Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = ticketId,
            Status = TicketStatus.WaitingForUser,
            Subtype = TicketSubtype.AccessRecovery,
            IsGuest = true
        });
        CommentsTaken();

        var act = () => service.CreateTrackedComment("tok123",
            new CreateTicketComment { Text = "Все, спасибо", Status = TicketStatus.Closed });

        await act.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);
    }

    /// <summary>
    /// The answer of moderation is news to an author with an account; a guest
    /// reads it on his tracking page, and nobody is told of a comment refused.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnnounceTheAnswerOfModerationToAnAuthorWithAnAccount(bool hasAccount)
    {
        OpenTicketTakingComments(hasAccount ? Guid.NewGuid() : null);

        await service.CreateComment(ticketId, new CreateTicketComment { Text = "Пришлите ссылку" });

        if (hasAccount)
        {
            await eventProducer.Received(1).SendAsync(EventType.TicketAnswered, commentId);
        }
        else
        {
            await eventProducer.DidNotReceive().SendAsync(EventType.TicketAnswered, Arg.Any<Guid>());
        }

        await eventProducer.DidNotReceive().SendAsync(EventType.TicketReplied, Arg.Any<Guid>());
    }

    /// <summary>
    /// The repository decides whether the comment lands: a ticket closed beside
    /// the comment and a conversation at its ceiling both refuse it, and nothing
    /// is announced.
    /// </summary>
    [Theory]
    [InlineData(TicketCommentOutcome.Closed, "Обращение закрыто")]
    [InlineData(TicketCommentOutcome.Full, "100 ответов")]
    public async Task RefuseACommentTheRepositoryDidNotTake(TicketCommentOutcome outcome, string refusal)
    {
        OpenTicketTakingComments(Guid.NewGuid());
        ticketRepository.AddComment(Arg.Any<CreateTicketCommentEntity>(), Arg.Any<TicketStatus>(),
                Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((outcome, (TicketComment?)null));

        var act = () => service.CreateComment(ticketId, new CreateTicketComment { Text = "Еще одно" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest && e.Message.Contains(refusal));
        await eventProducer.DidNotReceiveWithAnyArgs().SendAsync(default(EventType), default(Guid));
    }

    /// <summary>
    /// The ceiling the repository holds is the hundred of the conversation.
    /// </summary>
    [Fact]
    public async Task HoldTheConversationToAHundredComments()
    {
        OpenTicketTakingComments(Guid.NewGuid());

        await service.CreateComment(ticketId, new CreateTicketComment { Text = "Ответ" });

        await ticketRepository.Received(1).AddComment(Arg.Any<CreateTicketCommentEntity>(),
            Arg.Any<TicketStatus>(), TicketTextLimits.MaxComments, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A closed ticket takes no comment, and the form is not offered on it; a full
    /// conversation offers no form either.
    /// </summary>
    [Fact]
    public async Task OfferTheFormOnlyWhereACommentLands()
    {
        ticketRepository.Get(ticketId, Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = ticketId,
            ReporterUserId = currentUserId,
            Status = TicketStatus.Closed,
            Subtype = TicketSubtype.UserComplaint
        });

        (await service.GetTicket(ticketId)).CanComment.Should().BeFalse();
        var act = () => service.CreateComment(ticketId, new CreateTicketComment { Text = "А еще" });
        await act.Should().ThrowAsync<HttpException>().Where(e => e.StatusCode == HttpStatusCode.BadRequest);
        await ticketRepository.DidNotReceiveWithAnyArgs().AddComment(default!, default, default, default);

        OpenTicketTakingComments(currentUserId);
        ticketRepository.GetComments(ticketId, Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(0, TicketTextLimits.MaxComments).Select(_ => new TicketComment()).ToArray());
        (await service.GetTicket(ticketId)).CanComment.Should().BeFalse();
    }

    /// <summary>
    /// The guest page offers what the guest may do and nothing of moderation's,
    /// whoever is signed in while reading it.
    /// </summary>
    [Fact]
    public async Task OfferTheHolderOfTheTokenTheFormAndNoClosing()
    {
        SetCurrentUser(UserRole.Admin);
        ticketRepository.GetByTrackingToken("tok123", Arg.Any<CancellationToken>()).Returns(new Ticket
        {
            TicketId = ticketId,
            Status = TicketStatus.WaitingForModeration,
            Subtype = TicketSubtype.UserComplaint,
            TargetUserId = targetUserId
        });

        var page = await service.GetTicketByTrackingToken("tok123");

        page!.CanComment.Should().BeTrue();
        page.CanResolve.Should().BeFalse();
    }
}
