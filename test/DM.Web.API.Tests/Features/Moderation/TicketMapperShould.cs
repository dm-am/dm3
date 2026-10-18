using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Tickets;
using DM.Web.API.Shared.BbRendering;
using DM.Testing;
using AwesomeAssertions;
using Xunit;
using DomainTicket = DM.Domain.Moderation.Features.Tickets.Ticket;
using DomainTicketComment = DM.Domain.Moderation.Features.Tickets.TicketComment;
using DomainTicketDetails = DM.Domain.Moderation.Features.Tickets.TicketDetails;

namespace DM.Web.API.Tests.Features.Moderation;

/// <summary>
/// The support form writes the ticket in the site markup, and the three screens
/// that read it back (the moderation card, "Мои обращения" and the moderation
/// queues) print it. The stored text goes out as written, for the author and
/// for moderation to read the source, and once more in a carrier the serializer
/// renders, so no screen has to print BBCode. The conversation goes out rendered.
/// </summary>
public class TicketMapperShould : UnitTestBase
{
    private const string Body = "[b]Страница[/b] не открывается";

    private TicketMapper Mapper() => new(new UserMapper(Mock<IImgproxyUrlBuilder>()));

    [Fact]
    public void SendTheListRowBodyAsWrittenAndForRendering()
    {
        var result = Mapper().ToTicket(new DomainTicket
        {
            TicketId = Guid.NewGuid(),
            Description = Body
        });

        result.Description.Should().Be(Body);
        result.DescriptionHtml.Should().BeOfType<InfoBbText>()
            .Which.Value.Should().Be(Body);
    }

    /// <summary>
    /// A comment of the conversation is written in the editor of comments and
    /// goes out in a carrier the serializer renders with that tag set, [mod]
    /// included.
    /// </summary>
    [Fact]
    public void SendACommentOfTheConversationForRendering()
    {
        const string text = "[mod]Разобрались[/mod]";
        var commentId = Guid.NewGuid();

        var result = Mapper().ToTicketComment(new DomainTicketComment
        {
            TicketCommentId = commentId,
            Author = new GeneralUser { UserId = Guid.NewGuid(), Username = "TestModerator" },
            Text = text
        });

        result.Id.Should().Be(commentId);
        result.Author!.Username.Should().Be("TestModerator");
        result.IsGuest.Should().BeFalse();
        result.Text.Should().BeOfType<CommonBbText>()
            .Which.Value.Should().Be(text);
    }

    /// <summary>
    /// A comment of the guest is signed by nobody, and says so: a missing author
    /// alone would read as a removed account.
    /// </summary>
    [Fact]
    public void SendTheCommentOfTheGuestWithoutAnAuthor()
    {
        var result = Mapper().ToTicketComment(new DomainTicketComment
        {
            TicketCommentId = Guid.NewGuid(),
            IsGuest = true,
            Text = "Уточню"
        });

        result.Author.Should().BeNull();
        result.IsGuest.Should().BeTrue();
    }

    /// <summary>
    /// The page of a ticket carries the conversation, what the caller may do in
    /// it, and who closed it.
    /// </summary>
    [Fact]
    public void SendThePageWithTheConversationAndWhatTheCallerMayDo()
    {
        var ticketId = Guid.NewGuid();

        var result = Mapper().ToTicketDetails(new DomainTicketDetails
        {
            TicketId = ticketId,
            Description = Body,
            Reporter = new GeneralUser { UserId = Guid.NewGuid(), Username = "Experienced" },
            CommentsCount = 1,
            Comments = [new DomainTicketComment { TicketCommentId = Guid.NewGuid(), Text = "Ответ" }],
            ResolvedByUsername = "TestModerator",
            CanComment = true,
            CanResolve = true
        });

        result.Id.Should().Be(ticketId);
        result.Reporter!.Username.Should().Be("Experienced");
        result.Comments.Should().ContainSingle();
        result.CommentsCount.Should().Be(1);
        result.ResolvedByUsername.Should().Be("TestModerator");
        result.CanComment.Should().BeTrue();
        result.CanResolve.Should().BeTrue();
        result.DescriptionHtml.Value.Should().Be(Body);
    }

    /// <summary>
    /// The answer carries the status moderation chose with it, and none when it
    /// chose none: the service reads a missing status as the author's answer or
    /// as "waiting for the user".
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(TicketStatus.Closed)]
    public void SendTheStatusOfAnAnswerAsChosen(TicketStatus? status)
    {
        var result = Mapper().ToCreateTicketComment(new AnswerTicketRequest { Text = "Ответ", Status = status });

        result.Text.Should().Be("Ответ");
        result.Status.Should().Be(status);
    }

    /// <summary>
    /// The guest answers by his token as the author, and the author chooses no
    /// status.
    /// </summary>
    [Fact]
    public void SendNoStatusWithTheAnswerOfTheGuest()
    {
        var result = Mapper().ToCreateTicketComment(new CreateTicketCommentRequest { Text = "Уточню" });

        result.Text.Should().Be("Уточню");
        result.Status.Should().BeNull();
    }

    /// <summary>
    /// A guest ticket and the ticket of a removed account both have no reporter;
    /// the flag is what the signature reads "гость" by.
    /// </summary>
    [Fact]
    public void TellAGuestTicketFromTheTicketOfARemovedAccount()
    {
        var guest = Mapper().ToTicket(new DomainTicket { TicketId = Guid.NewGuid(), IsGuest = true });
        var removed = Mapper().ToTicket(new DomainTicket { TicketId = Guid.NewGuid(), IsGuest = false });

        guest.Reporter.Should().BeNull();
        guest.IsGuest.Should().BeTrue();
        removed.Reporter.Should().BeNull();
        removed.IsGuest.Should().BeFalse();
    }
}
