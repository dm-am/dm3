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

public class TelegramWebhookHandlerShould : UnitTestBase
{
    private readonly Mock<IBotLinkService> _botLinkService;
    private readonly TelegramWebhookHandler _handler;

    public TelegramWebhookHandlerShould()
    {
        _botLinkService = Mock<IBotLinkService>();
        _botLinkService
            .Setup(s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotLinkResult { Success = true, Username = "CurrentUser" });

        _handler = new TelegramWebhookHandler(
            _botLinkService.Object,
            Mock<ILogger<TelegramWebhookHandler>>().Object);
    }

    private static JsonElement Payload(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    [Fact]
    public void HaveTelegramBotType()
    {
        _handler.BotType.Should().Be("telegram");
    }

    [Fact]
    public async Task VerifyAndLinkOnConnectCommand()
    {
        var payload = Payload("""{"message": {"text": "/connect ABC123", "chat": {"id": 12345}}}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(s => s.VerifyAndLink("ABC123", "telegram", "12345", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TrimCodeFromConnectCommand()
    {
        var payload = Payload("""{"message": {"text": "/connect  abc123 ", "chat": {"id": 12345}}}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(s => s.VerifyAndLink("abc123", "telegram", "12345", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IgnoreUpdateWithoutMessage()
    {
        var payload = Payload("""{"callback_query": {"id": "1"}}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(
            s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IgnoreMessageWithoutText()
    {
        var payload = Payload("""{"message": {"chat": {"id": 12345}}}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(
            s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task IgnoreMessageWithoutChatId()
    {
        var payload = Payload("""{"message": {"text": "/connect ABC123", "chat": {}}}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(
            s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotLinkOnStartCommand()
    {
        var payload = Payload("""{"message": {"text": "/start", "chat": {"id": 12345}}}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(
            s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotLinkOnUnrelatedText()
    {
        var payload = Payload("""{"message": {"text": "hello there", "chat": {"id": 12345}}}""");

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(
            s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
