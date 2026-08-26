using System.Collections.Generic;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Workers.NotificationDispatcher.Bot;
using DM.Workers.NotificationDispatcher.Email;
using AwesomeAssertions;
using Xunit;
using DM.Workers.NotificationDispatcher.Dispatching;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// Metadata of a notification is text a user typed, and two of the three channels
/// put it into markup.
/// </summary>
/// <remarks>
/// A game titled with an anchor tag used to reach the reader as a working link to
/// another site, inside a letter signed dm.am. The same title carrying an angle
/// bracket made the Telegram message invalid markup: Telegram answered 400 and the
/// notification was lost behind a warning in the log, which is a failure nobody is
/// told about. The third channel is here because it is the reason the two others
/// cannot share one message.
///
/// The escaped forms are written out rather than produced by an encoder here: a
/// test that escapes the same way the code does passes whatever the code does.
/// </remarks>
public class NotificationMarkupShould
{
    private const EventType Event = EventType.NewGameFromSubscribedAuthor;

    private const string HostileTitle = "<a href=\"https://evil.example\">Смените пароль</a>";

    private const string EscapedTitle =
        "&lt;a href=&quot;https://evil.example&quot;&gt;Смените пароль&lt;/a&gt;";

    private const string HostileName = "Tom & \"Jerry\"";

    private const string EscapedName = "Tom &amp; &quot;Jerry&quot;";

    /// <summary>The addresses of the site, as configuration gives them.</summary>
    private static readonly SiteAddressConfiguration Addresses = new()
    {
        PublicUrl = "https://example.test"
    };

    /// <summary>A game title and a username are whatever their owner typed.</summary>
    private static readonly object Metadata = new
    {
        GameTitle = HostileTitle,
        AuthorUsername = HostileName
    };

    [Fact]
    public void KeepMetadataOutOfTheLetterAsMarkup()
    {
        var body = NotificationEmailSender.BuildEmailBody(Event, Metadata, Addresses);

        body.Should().NotContain(HostileTitle,
            "a title shaped like a link becomes a working link to another site in a letter signed dm.am");
        body.Should().NotContain(HostileName);
        body.Should().Contain(EscapedTitle);
        body.Should().Contain(EscapedName);
        body.Should().Contain("Смените пароль",
            "escaping must not turn Russian text into numeric references");
    }

    /// <summary>
    /// The footer names every address the site answers on.
    /// </summary>
    /// <remarks>
    /// A mailbox is the only place a reader can still be reached once the address
    /// he uses stops answering, and this is the letter he gets most often. The list
    /// used to be deployment configuration, and no deployment path ever filled it
    /// in — the line was empty everywhere.
    ///
    /// The public address of the fixture is a host of nobody on purpose: taking the
    /// list from the deployment instead would pass while naming whatever the stand
    /// happens to answer on.
    /// </remarks>
    [Fact]
    public void NameEveryAddressOfTheSiteInTheFooter()
    {
        var body = NotificationEmailSender.BuildEmailBody(Event, Metadata, Addresses);

        foreach (var host in DM.Domain.Core.Site.SiteAddresses.Hosts)
        {
            body.Should().Contain(host);
        }
    }

    [Fact]
    public void KeepMetadataOutOfTheTelegramMessageAsMarkup()
    {
        var message = NotificationBotSender.BuildTelegramMessage(Event, Metadata, Addresses);

        message.Should().NotContain(HostileTitle,
            "the message is sent with parse_mode HTML, and one angle bracket in it costs the whole message");
        message.Should().NotContain(HostileName);
        message.Should().Contain(EscapedTitle);
        message.Should().Contain(EscapedName);
    }

    [Fact]
    public void SendDiscordTextRatherThanMarkup()
    {
        var message = NotificationBotSender.BuildDiscordMessage(Event, Metadata, Addresses);

        message.Should().NotContain("<b>", "Discord prints the content as text, so the tags would show");
        message.Should().NotContain("&amp;", "and so would the entities");
        message.Should().Contain(HostileTitle, "nothing parses it, so nothing has to be escaped");
    }

    [Fact]
    public void SayNothingAboutAMetadataBagThatIsNotAnObject()
    {
        var body = NotificationEmailSender.BuildEmailBody(Event, "{}", Addresses);

        body.Should().NotContain("<pre",
            "the letter used to fall back to dumping the serialized bag into itself");
        body.Should().Contain("Новая игра от подписанного автора");
    }
}
