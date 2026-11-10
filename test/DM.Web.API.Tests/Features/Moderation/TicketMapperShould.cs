using System;
using DM.Domain.Core.Dto;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Moderation.Tickets;
using DM.Web.API.Shared.BbRendering;
using DM.Testing;
using AwesomeAssertions;
using Xunit;
using DomainTicket = DM.Domain.Moderation.Features.Tickets.Ticket;
using DomainTicketComment = DM.Domain.Moderation.Features.Tickets.TicketComment;

namespace DM.Web.API.Tests.Features.Moderation;

public class TicketMapperShould : UnitTestBase
{
    private const string Body = "[b]Страница[/b] не открывается";

    private static TicketMapper Mapper() => new(new UserMapper());

    [Fact]
    public void SendTheListRowBodyForRendering()
    {
        var result = Mapper().ToTicket(new DomainTicket
        {
            TicketId = Guid.NewGuid(),
            Description = Body
        });

        result.Description.Should().BeOfType<InfoBbText>()
            .Which.Value.Should().Be(Body);
    }

    // A comment of the conversation is written in the editor of comments and goes out in a carrier
    // the serializer renders with that tag set, [mod] included.
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

    // A comment of the guest is signed by nobody, and says so: a missing author alone would read as
    // a removed account.
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

    // A guest ticket and the ticket of a removed account both have no reporter; the flag is what
    // the signature reads "гость" by.
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
