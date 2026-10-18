using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Notifications;
using DM.Workers.NotificationDispatcher.Notifiers.Moderation;
using AwesomeAssertions;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// Who is told that a ticket was filed.
/// </summary>
/// <remarks>
/// The notification carries the text of the ticket and the name of its author, so
/// its audience is the audience of the ticket itself: the moderators whose role
/// reads its category, less the moderator it names and the moderator who filed it.
/// Every moderator on duty was told before, and a junior moderator read the text
/// of an access recovery request he cannot open, and a moderator read a complaint
/// about himself together with the name of whoever filed it.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class TicketCreatedNotificationsShould
{
    private readonly NotificationDatabaseFixture _fixture;

    public TicketCreatedNotificationsShould(NotificationDatabaseFixture fixture) =>
        _fixture = fixture;

    private async Task<Guid[]> Told(Guid ticketId)
    {
        await TicketSeed.ApplyAsync(_fixture);
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new TicketCreatedNotificationGenerator(context), ticketId);
        return TicketSeed.Recipients(produced.ToArray());
    }

    [Fact]
    public async Task KeepAnAdministratorsCategoryFromTheModerators()
    {
        var told = await Told(TicketSeed.AccessRecoveryTicketId);

        told.Should().Contain(TicketSeed.AdminId,
            "an access recovery request is the administrators' to read");
        told.Should().NotContain(TicketSeed.JuniorModeratorId,
            "a junior moderator cannot open an access recovery request, so its text is not his to read");
        told.Should().NotContain(TicketSeed.SeniorModeratorId,
            "a senior moderator cannot open one either");
    }

    [Fact]
    public async Task KeepAComplaintFromTheModeratorItNames()
    {
        var told = await Told(TicketSeed.ComplaintAboutAModeratorId);

        told.Should().Contain(new[]
            {
                TicketSeed.JuniorModeratorId, TicketSeed.SeniorModeratorId,
                TicketSeed.AdminId, TicketSeed.FilingModeratorId
            },
            "every other moderator reads a complaint about a user");
        told.Should().NotContain(TicketSeed.NamedModeratorId,
            "the moderator a complaint names is an ordinary user to it, and an ordinary user " +
            "never learns of a complaint about himself, let alone who filed it");
    }

    [Fact]
    public async Task KeepAComplaintFromTheModeratorWhoFiledIt()
    {
        var told = await Told(TicketSeed.ComplaintByAModeratorId);

        told.Should().Contain(TicketSeed.JuniorModeratorId,
            "the complaint is a user complaint, which the other moderators read");
        told.Should().NotContain(TicketSeed.FilingModeratorId,
            "a filed ticket is no news to the moderator who filed it");
    }

    [Fact]
    public async Task TellNobodyButTheModeratorsOnDuty()
    {
        var told = await Told(TicketSeed.ComplaintAboutAModeratorId);

        told.Should().NotContain(TicketSeed.ReaderId, "a reader is no moderator");
        told.Should().NotContain(TicketSeed.DemotedId, "a former moderator is no moderator either");
        told.Should().NotContain(TicketSeed.RemovedModeratorId, "a removed account reads nothing");
        told.Should().NotContain(SystemUser.Id, "the robot account cannot read");
        told.Should().OnlyHaveUniqueItems("one filing is one notification to each moderator");
    }

    [Fact]
    public void TitleTheFilingInWords()
    {
        NotificationWording.GetTitle(EventType.TicketCreated).Should().Be("Новое обращение");
    }
}
