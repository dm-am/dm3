using System;
using System.Linq;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Domain.Moderation.Features.Tickets;
using DM.Tools.Seeder.Seeding;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The seed holds a ticket whose conversation took all it takes, so the thread shows the line of a
/// full conversation where the answer form stands.
/// </summary>
/// <remarks>
/// The line stands for an open ticket with no room left (TicketService.Detailed), so the ticket has to be
/// both, and it must not change what the queues of the moderators open with.
/// </remarks>
public class SeededFullTicketShould
{
    private static readonly DateTimeOffset Epoch = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Member = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static readonly Guid Admin = Guid.Parse("10000000-0000-0000-0000-000000000002");

    [Fact]
    public void TakeAllTheConversationTakesAndWaitForTheMember()
    {
        var (ticket, comments) = DataSeeder.FullConversation(Member, Admin, Epoch);

        comments.Should().HaveCount(TicketTextLimits.MaxComments,
            "one comment short of the ceiling the thread still offers the answer form");
        ticket.Status.Should().Be(TicketStatus.WaitingForUser,
            "a closed ticket hides the form for a reason of its own and says that one instead");
        comments.Select(c => c.AuthorId).Should().Equal(
            Enumerable.Range(1, comments.Count).Select(n => (Guid?)(n % 2 == 1 ? Member : Admin)),
            "the member and the administrator answer each other, and the last word, the administrator's, " +
            "is what the ticket waits for the member after");
        comments.Should().OnlyContain(c => c.TicketId == ticket.TicketId);
        comments.Should().BeInAscendingOrder(c => c.CreatedUtc);
        comments[0].CreatedUtc.Should().BeAfter(ticket.CreatedUtc);
        comments[^1].CreatedUtc.Should().BeBefore(Epoch, "the seed writes no future");
        ticket.UpdatedUtc.Should().Be(comments[^1].CreatedUtc, "a comment moves the ticket, as AddComment does");
    }

    [Fact]
    public void LeaveTheShortConversationsAndTheModeratorsQueuesAsTheyWere()
    {
        var (ticket, comments) = DataSeeder.FullConversation(Member, Admin, Epoch);

        var shortConversation = Enumerable.Range(1, 6)
            .Select(n => Guid.Parse($"00000000-0000-0000-00d6-{n:x12}"));
        comments.Select(c => c.TicketCommentId).Should().OnlyHaveUniqueItems()
            .And.NotContain(shortConversation, "the six comments of the short conversations keep their numbers");

        TicketVisibility.Subtypes(UserRole.SeniorModerator).Should().NotContain(ticket.Subtype,
            "the stand opens the first ticket of the moderator's queue, and a category only administrators " +
            "read keeps this one out of it");
    }
}
