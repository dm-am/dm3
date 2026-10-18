using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Notifiers.Moderation;
using AwesomeAssertions;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// Who is told that moderation wrote in the conversation of a ticket, what he is
/// told, and where it leads him.
/// </summary>
/// <remarks>
/// The event names the comment, and the comment names two people: the moderator
/// who wrote it and, through its ticket, the author it answers. Only the author is
/// the reader, and only with an account: a guest reads the answer on his tracking
/// page.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class TicketAnsweredNotificationsShould
{
    private readonly NotificationDatabaseFixture _fixture;

    public TicketAnsweredNotificationsShould(NotificationDatabaseFixture fixture) =>
        _fixture = fixture;

    private async Task<CreateNotification[]> Answered(Guid commentId)
    {
        await TicketSeed.ApplyAsync(_fixture);
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new TicketAnsweredNotificationGenerator(context), commentId);
        return produced.ToArray();
    }

    [Fact]
    public async Task TellTheAuthorOfTheTicketOnly()
    {
        var produced = await Answered(TicketSeed.SeniorAnswerId);

        TicketSeed.Recipients(produced).Should().Equal(new[] { TicketSeed.ReaderId },
            "the answer passes the turn to the author, and to nobody else on the row");
    }

    [Fact]
    public async Task NameTheTicketAndTheModeratorWhoAnswered()
    {
        var notification = (await Answered(TicketSeed.SeniorAnswerId)).Single();

        TicketSeed.Field(notification, "TicketId").Should().Be(TicketSeed.ComplaintAboutAModeratorId,
            "the event names the comment, and the page is the page of its ticket");
        TicketSeed.Field(notification, "ModeratorUsername").Should().Be(TicketSeed.SeniorModeratorUsername,
            "the letter names the moderator who answered");
    }

    [Fact]
    public async Task TellAModeratorWhoFiledATicketAsItsAuthor()
    {
        var produced = await Answered(TicketSeed.JuniorAnswerId);

        TicketSeed.Recipients(produced).Should().Equal(new[] { TicketSeed.FilingModeratorId },
            "a moderator who filed a ticket is its author, and an answer is news to him");
    }

    [Fact]
    public async Task TellAGuestNothing()
    {
        var produced = await Answered(TicketSeed.AdminAnswerToTheGuestId);

        produced.Should().BeEmpty("a guest has no inbox; his tracking page carries the answer");
    }

    [Fact]
    public async Task TellTheAuthorNothingOfHisOwnComment()
    {
        var produced = await Answered(TicketSeed.ReaderReplyId);

        produced.Should().BeEmpty(
            "a comment of the author is no answer to him, whatever event it arrived under");
    }

    [Fact]
    public void TitleTheAnswerInWords()
    {
        NotificationWording.GetTitle(EventType.TicketAnswered).Should().Be("Модерация ответила на обращение");
    }

    [Fact]
    public void LeadToThePageOfTheTicketItsAuthorReads()
    {
        var url = NotificationLink.GetUrl(
            EventType.TicketAnswered,
            new { TicketId = TicketSeed.ComplaintAboutAModeratorId },
            new SiteAddressConfiguration { PublicUrl = "https://example.test/" });

        url.Should().Be($"https://example.test/my-tickets/{TicketSeed.ComplaintAboutAModeratorId}");
    }

    [Fact]
    public void ReachTheModerationCategory()
    {
        NotificationCategoryMapper.GetCategory(EventType.TicketAnswered).Should().Be(NotificationCategory.Moderation,
            "a category is the permission to leave the site: without one the answer reaches " +
            "neither the letter nor the bots");
    }
}
