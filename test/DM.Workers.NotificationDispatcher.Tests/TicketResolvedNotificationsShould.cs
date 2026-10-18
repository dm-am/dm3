using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Notifiers.Moderation;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// Who is told that moderation closed a ticket, what he is told, and where it
/// leads him.
/// </summary>
/// <remarks>
/// The row names two people at most - the author and the moderator who closed
/// it - and only the first is the reader. A guest is nobody to the inbox: his
/// tracking page carries the closing instead.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class TicketResolvedNotificationsShould
{
    private readonly NotificationDatabaseFixture _fixture;

    public TicketResolvedNotificationsShould(NotificationDatabaseFixture fixture) =>
        _fixture = fixture;

    /// <summary>Filed by a reader, answered by the master of the seed.</summary>
    private static readonly Guid AnsweredTicketId =
        Guid.Parse("00000000-0000-0000-0000-0000000000f2");

    /// <summary>Filed by a guest, answered all the same.</summary>
    private static readonly Guid GuestTicketId =
        Guid.Parse("00000000-0000-0000-0000-0000000000f3");

    private static readonly DateTimeOffset Filed = DateTimeOffset.UtcNow.AddDays(-2);

    /// <summary>The two tickets, written once into the shared database.</summary>
    private async Task Seed()
    {
        await using var context = _fixture.CreateContext();
        if (await context.Tickets.AnyAsync(t => t.TicketId == AnsweredTicketId))
        {
            return;
        }

        context.Tickets.AddRange(
            Answered(AnsweredTicketId, NotificationSeed.WaitedForUserId, guestEmail: null),
            Answered(GuestTicketId, null, guestEmail: "guest@example.com"));
        await context.SaveChangesAsync();
    }

    private static Ticket Answered(Guid ticketId, Guid? authorId, string? guestEmail) => new()
    {
        TicketId = ticketId,
        UserId = authorId,
        GuestEmail = guestEmail,
        EntityType = "",
        Status = TicketStatus.Closed,
        Subtype = TicketSubtype.Bug,
        CreatedUtc = Filed,
        Description = "Страница не открывается",
        Comment = "",
        // The letter names the moderator who closed it
        ResolvedById = NotificationSeed.MasterId,
        ResolvedUtc = Filed.AddHours(3)
    };

    private async Task<CreateNotification[]> Resolved(Guid ticketId)
    {
        await Seed();
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new TicketResolvedNotificationGenerator(context), ticketId);
        return produced.ToArray();
    }

    /// <summary>Reads a field off the anonymous metadata object.</summary>
    private static object? Field(CreateNotification notification, string name)
    {
        var property = notification.Metadata.GetType().GetProperty(name);
        property.Should().NotBeNull($"the metadata carries {name}");
        return property!.GetValue(notification.Metadata);
    }

    [Fact]
    public async Task TellTheAuthorOfTheTicket()
    {
        var produced = await Resolved(AnsweredTicketId);

        produced.Should().ContainSingle();
        produced[0].UsersInterested.Should().Equal(new[] { NotificationSeed.WaitedForUserId },
            "the answer is news to the author, and neither moderator on the row is him");
    }

    [Fact]
    public async Task NameTheTicketAndTheModeratorWhoAnswered()
    {
        var notification = (await Resolved(AnsweredTicketId)).Single();

        Field(notification, "TicketId").Should().Be(AnsweredTicketId,
            "the page of the ticket is built out of this field alone");
        Field(notification, "ModeratorUsername").Should().Be(NotificationSeed.MasterUsername,
            "the letter names the moderator who closed the ticket");
    }

    [Fact]
    public async Task TellAGuestNothing()
    {
        var produced = await Resolved(GuestTicketId);

        produced.Should().BeEmpty("a guest has no inbox; his tracking page carries the answer");
    }

    [Fact]
    public void TitleTheClosingInWords()
    {
        // Moderation answers in the conversation now, and that answer has a title of
        // its own (TicketAnsweredNotificationsShould): this one says the ticket is over.
        NotificationWording.GetTitle(EventType.TicketResolved).Should().Be("Обращение закрыто");
    }

    [Fact]
    public void LeadToThePageOfTheTicket()
    {
        var url = NotificationLink.GetUrl(
            EventType.TicketResolved,
            new { TicketId = AnsweredTicketId },
            new SiteAddressConfiguration { PublicUrl = "https://example.test/" });

        url.Should().Be($"https://example.test/my-tickets/{AnsweredTicketId}");
    }
}
