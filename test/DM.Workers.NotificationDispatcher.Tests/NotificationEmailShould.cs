using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Mail;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Mail.Rendering;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Workers.NotificationDispatcher.Email;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A notification sent by mail goes out as every other letter of the site: under the site's subject, in the
/// common frame, with the logo the frame shows.
/// </summary>
/// <remarks>The template and the logo are the mail module's own; only the relay is a substitute.</remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class NotificationEmailShould
{
    private static readonly SiteAddressConfiguration Addresses = new() { PublicUrl = "https://dm.am" };

    private readonly NotificationDatabaseFixture _fixture;

    public NotificationEmailShould(NotificationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GoOutAsTheOtherLettersOfTheSite()
    {
        var readerId = Guid.NewGuid();
        await using (var seed = _fixture.CreateContext())
        {
            seed.Users.Add(NotificationSeed.User(readerId, "letter-reader", DateTimeOffset.UtcNow.AddDays(-1)));
            var settings = UserSettings.CreateDefault(readerId);
            settings.EmailPreferences = new NotificationChannelPreference { EnabledCategories = [NotificationCategory.Games] };
            seed.UserSettings.Add(settings);
            await seed.SaveChangesAsync();
        }

        EmailLetter? sent = null;
        var relay = Substitute.For<IMailSender>();
        relay.SendAsync(Arg.Do<EmailLetter>(letter => sent = letter)).Returns(Task.CompletedTask);

        await using var mail = new ServiceCollection()
            .AddLogging()
            .AddSingleton(Options.Create(Addresses))
            .AddDmMail()
            .BuildServiceProvider();
        await using var context = _fixture.CreateContext();
        var sender = new NotificationEmailSender(context, relay,
            mail.GetRequiredService<ITemplateRenderer>(), mail.GetRequiredService<IEmailAssetsProvider>(),
            Options.Create(Addresses), NullLogger<NotificationEmailSender>.Instance);

        await sender.SendIfEnabled(new CreateNotification
        {
            UsersInterested = [readerId],
            EventType = EventType.StatusGameFrozen,
            Metadata = new { GameTitle = "Потерянные хроники", GameId = "poterjannye-hroniki~aBcD" },
        }, EventType.StatusGameFrozen, CancellationToken.None);

        sent.Should().NotBeNull("the reader takes the letters about games");
        sent!.Subject.Should().Be("Dungeon Master: Игра заморожена");
        sent.Body.Should().Contain(EmailConstants.CardStyles, "the card every other letter is drawn in");
        sent.Body.Should().Contain("Потерянные хроники");
        sent.LinkedResources.Should().ContainSingle(resource => resource.ContentId == "logo",
            "the frame shows the logo by its cid, and a letter without the part shows a broken picture");
    }
}
