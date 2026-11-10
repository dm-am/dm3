using System;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail.ViewModels;
using DM.Domain.Core.Site;
using DM.Infrastructure.Mail.Rendering;
using AwesomeAssertions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DM.Infrastructure.Mail.Tests;

// Two lines are its own: the addresses of the site, and how to stop the letters.
public class NotificationLetterShould : IAsyncDisposable
{
    public static readonly NotificationLetterViewModel AboutAGame = new(
        "Игра заморожена",
        [new("Игра", "<a href=\"https://evil.example\">Смените пароль</a>"), new("Мастер", "Аллигатор")],
        "https://dm.am/game/hroniki~aBcD",
        "Перейти");

    private readonly HtmlRenderer _htmlRenderer = EmailRendering.CreateHtmlRenderer(
        NullLoggerFactory.Instance, new SiteAddressConfiguration { PublicUrl = "https://dm.am" });

    private Task<string> Render(NotificationLetterViewModel model) =>
        new TemplateRenderer(_htmlRenderer).RenderAsync(model);

    [Fact]
    public async Task StandOnTheFrameOfTheOtherLetters()
    {
        var html = await Render(AboutAGame);

        html.Should().Contain(EmailConstants.CardStyles, "the card every other letter is drawn in");
        html.Should().Contain("cid:logo");
        html.Should().Contain("Игра заморожена");
        html.Should().NotContain("#4a90d9", "the colours are the theme's, not a frame of its own");
    }

    [Fact]
    public async Task PutTheFactsInTheBoxOfTheSecurityLetters()
    {
        var html = await Render(AboutAGame);

        html.Should().Contain(EmailConstants.BoxStyles);
        html.Should().Contain(">Мастер</td>");
        html.Should().Contain(">Аллигатор</td>");
    }

    [Fact]
    public async Task LeadToThePageByTheButtonOfTheOtherLetters()
    {
        var html = await Render(AboutAGame);

        html.Should().Contain(EmailConstants.ButtonStyles);
        html.Should().Contain($"href=\"{AboutAGame.LinkUrl}\"");
        html.Should().Contain(">Перейти</a>");
    }

    [Fact]
    public async Task KeepTheAddressesOfTheSiteAndTheWayToStopTheLetters()
    {
        var html = await Render(AboutAGame);

        html.Should().Contain("Сайт открывается по адресам: " + string.Join(", ", SiteAddresses.Hosts));
        html.Should().Contain("Вы можете отключить уведомления на почту в настройках профиля.");
    }

    // The values of a notification are what their owners typed, and reach the letter as text.
    [Fact]
    public async Task EscapeWhatTheNotificationCarries()
    {
        var html = await Render(AboutAGame);

        html.Should().NotContain("<a href=\"https://evil.example\">",
            "a title shaped like a link becomes a working link to another site in a letter signed dm.am");
        html.Should().Contain("&lt;a href=&quot;https://evil.example&quot;&gt;Смените пароль&lt;/a&gt;");
    }

    [Fact]
    public async Task OfferNoButtonAndNoBoxWithNothingToShow()
    {
        var html = await Render(AboutAGame with { LinkUrl = null, Facts = [] });

        html.Should().NotContain("Перейти", "an offer to go somewhere is worth nothing when there is nowhere to go");
        html.Should().NotContain(EmailConstants.BoxStyles, "an empty box under the title looks broken");
    }

    public async ValueTask DisposeAsync()
    {
        await _htmlRenderer.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
