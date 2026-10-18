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
/// Who is told that the author of a ticket wrote in its conversation, what they
/// are told, and where it leads them.
/// </summary>
/// <remarks>
/// The moderators already in the conversation, and of them the ones the ticket is
/// still moderation's business of: not a moderator who never wrote, not one who
/// lost the rank since, not the violator, not the author himself when he is a
/// moderator. Each of those is what a generator answering "every moderator" or
/// "everybody who wrote" would get wrong, and the seed has one of each.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class TicketRepliedNotificationsShould
{
    private readonly NotificationDatabaseFixture _fixture;

    public TicketRepliedNotificationsShould(NotificationDatabaseFixture fixture) =>
        _fixture = fixture;

    private async Task<CreateNotification[]> Replied(Guid commentId)
    {
        await TicketSeed.ApplyAsync(_fixture);
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new TicketRepliedNotificationGenerator(context), commentId);
        return produced.ToArray();
    }

    [Fact]
    public async Task TellTheModeratorsInTheConversationOnly()
    {
        var told = TicketSeed.Recipients(await Replied(TicketSeed.ReaderReplyId));

        // Beside the senior moderator, the conversation has a former moderator, the
        // violator and a removed account among its writers, and the junior
        // moderator and the administrator on duty never wrote in it.
        told.Should().Equal(new[] { TicketSeed.SeniorModeratorId },
            "the reply is news to the moderator who asked for it, and to nobody who " +
            "cannot open the ticket or never took part in it");
    }

    [Fact]
    public async Task KeepTheViolatorOut()
    {
        var told = TicketSeed.Recipients(await Replied(TicketSeed.ReaderReplyId));

        told.Should().NotContain(TicketSeed.NamedModeratorId,
            "the moderator a complaint names is an ordinary user to it, whatever put him " +
            "among its writers");
        told.Should().NotContain(TicketSeed.DemotedId,
            "a former moderator cannot open the page the notification leads to");
    }

    [Fact]
    public async Task LeaveOutTheModeratorWhoFiledTheTicket()
    {
        var told = TicketSeed.Recipients(await Replied(TicketSeed.FilingModeratorReplyId));

        told.Should().Equal(new[] { TicketSeed.JuniorModeratorId },
            "a moderator who filed a ticket writes in it as its author, and his reply is " +
            "news to the moderator who answered him, not to himself");
    }

    [Fact]
    public async Task TellOfTheReplyOfAGuest()
    {
        var notification = (await Replied(TicketSeed.GuestReplyId)).Single();

        notification.UsersInterested.Should().Equal(new[] { TicketSeed.AdminId },
            "a guest writes by his token, and the administrator who answered him reads the reply");
        TicketSeed.Field(notification, "TicketId").Should().Be(TicketSeed.GuestTicketId,
            "the event names the comment, and the page is the page of its ticket");
        TicketSeed.Field(notification, "AuthorUsername").Should().BeNull(
            "a guest has no name, and a letter prints nothing for an empty field");
    }

    [Fact]
    public async Task NameTheAuthorWhoReplied()
    {
        var notification = (await Replied(TicketSeed.ReaderReplyId)).Single();

        TicketSeed.Field(notification, "AuthorUsername").Should().Be(TicketSeed.ReaderUsername,
            "the letter names the author who replied");
    }

    [Fact]
    public void TitleTheReplyInWords()
    {
        NotificationWording.GetTitle(EventType.TicketReplied).Should().Be("Автор ответил в обращении");
    }

    [Fact]
    public void LeadToThePageOfTheTicketModerationReads()
    {
        var url = NotificationLink.GetUrl(
            EventType.TicketReplied,
            new { TicketId = TicketSeed.ComplaintAboutAModeratorId },
            new SiteAddressConfiguration { PublicUrl = "https://example.test/" });

        url.Should().Be($"https://example.test/moderation/tickets/{TicketSeed.ComplaintAboutAModeratorId}");
    }

    [Fact]
    public void ReachTheModerationCategory()
    {
        NotificationCategoryMapper.GetCategory(EventType.TicketReplied).Should().Be(NotificationCategory.Moderation,
            "a category is the permission to leave the site: without one the reply reaches " +
            "neither the letter nor the bots");
    }
}
