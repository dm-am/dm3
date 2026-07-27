using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Testing;
using DM.Web.API.Features.Personal.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class DiscordWebhookHandlerShould : UnitTestBase
{
    private readonly Mock<IBotLinkService> _botLinkService;
    private readonly DiscordWebhookHandler _handler;

    public DiscordWebhookHandlerShould()
    {
        _botLinkService = Mock<IBotLinkService>();
        _botLinkService
            .Setup(s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotLinkResult { Success = true, Username = "CurrentUser" });

        _handler = new DiscordWebhookHandler(
            _botLinkService.Object,
            Mock<ILogger<DiscordWebhookHandler>>().Object);
    }

    private static JsonElement Payload(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public void HaveDiscordBotType()
    {
        _handler.BotType.Should().Be("discord");
    }

    [Fact]
    public async Task VerifyAndLinkOnConnectCommand()
    {
        var payload = Payload("""{"content": "/connect ABC123", "author": {"id": "9876543210"}}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(s => s.VerifyAndLink("ABC123", "discord", "9876543210", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptNumericAuthorId()
    {
        var payload = Payload("""{"content": "/connect ABC123", "author": {"id": 9876543210}}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(s => s.VerifyAndLink("ABC123", "discord", "9876543210", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IgnorePayloadWithoutAuthor()
    {
        var payload = Payload("""{"content": "/connect ABC123"}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(
            s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotLinkOnUnrelatedText()
    {
        var payload = Payload("""{"content": "hello there", "author": {"id": "9876543210"}}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(
            s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
