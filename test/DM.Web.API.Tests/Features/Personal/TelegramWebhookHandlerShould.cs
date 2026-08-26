using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Testing;
using DM.Web.API.Features.Personal.Webhooks;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Features.Personal;

public class TelegramWebhookHandlerShould : UnitTestBase
{
    private readonly IBotLinkService _botLinkService;
    private readonly TelegramWebhookHandler _handler;

    public TelegramWebhookHandlerShould()
    {
        _botLinkService = Mock<IBotLinkService>();
        _botLinkService
            .VerifyAndLink(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BotLinkResult { Success = true, Username = "CurrentUser" });

        _handler = new TelegramWebhookHandler(
            _botLinkService,
            Mock<ILogger<TelegramWebhookHandler>>());
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

        await _botLinkService.Received(1).VerifyAndLink("ABC123", "telegram", "12345", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TrimCodeFromConnectCommand()
    {
        var payload = Payload("""{"message": {"text": "/connect  abc123 ", "chat": {"id": 12345}}}""");

        await _handler.HandleAsync(payload);

        await _botLinkService.Received(1).VerifyAndLink("abc123", "telegram", "12345", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IgnoreUpdateWithoutMessage()
    {
        var payload = Payload("""{"callback_query": {"id": "1"}}""");

        await _handler.HandleAsync(payload);

        await _botLinkService.DidNotReceive().VerifyAndLink(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IgnoreMessageWithoutText()
    {
        var payload = Payload("""{"message": {"chat": {"id": 12345}}}""");

        await _handler.HandleAsync(payload);

        await _botLinkService.DidNotReceive().VerifyAndLink(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IgnoreMessageWithoutChatId()
    {
        var payload = Payload("""{"message": {"text": "/connect ABC123", "chat": {}}}""");

        await _handler.HandleAsync(payload);

        await _botLinkService.DidNotReceive().VerifyAndLink(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotLinkOnStartCommand()
    {
        var payload = Payload("""{"message": {"text": "/start", "chat": {"id": 12345}}}""");

        await _handler.HandleAsync(payload);

        await _botLinkService.DidNotReceive().VerifyAndLink(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotLinkOnUnrelatedText()
    {
        var payload = Payload("""{"message": {"text": "hello there", "chat": {"id": 12345}}}""");

        await _handler.HandleAsync(payload);

        await _botLinkService.DidNotReceive().VerifyAndLink(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
