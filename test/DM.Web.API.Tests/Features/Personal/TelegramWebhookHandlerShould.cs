using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Testing;
using DM.Web.API.Features.Personal.Webhooks;
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

    /// <summary>
    /// The account page hands the code out as a link, t.me/{bot}?start={code},
    /// and Telegram delivers the press of Start in that chat as "/start {code}".
    /// </summary>
    [Theory]
    [InlineData("""{"message": {"text": "/connect ABC123", "chat": {"id": 12345}}}""", "ABC123")]
    [InlineData("""{"message": {"text": "/connect  abc123 ", "chat": {"id": 12345}}}""", "abc123")]
    [InlineData("""{"message": {"text": "/start ABC123", "chat": {"id": 12345}}}""", "ABC123")]
    [InlineData("""{"message": {"text": "/start  abc123 ", "chat": {"id": 12345}}}""", "abc123")]
    public async Task VerifyAndLinkTheCodeOfTheCommand(string json, string code)
    {
        await _handler.HandleAsync(Payload(json));

        await _botLinkService.Received(1).VerifyAndLink(code, "telegram", "12345", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("""{"message": {"text": "/start   ", "chat": {"id": 12345}}}""")]
    [InlineData("""{"message": {"text": "/startABC123", "chat": {"id": 12345}}}""")]
    [InlineData("""{"callback_query": {"id": "1"}}""")]
    [InlineData("""{"message": {"chat": {"id": 12345}}}""")]
    [InlineData("""{"message": {"text": "/connect ABC123", "chat": {}}}""")]
    [InlineData("""{"message": {"text": "/start", "chat": {"id": 12345}}}""")]
    [InlineData("""{"message": {"text": "hello there", "chat": {"id": 12345}}}""")]
    public async Task NotLinkAnUpdateWithoutACommandACodeAndAChat(string json)
    {
        await _handler.HandleAsync(Payload(json));

        await _botLinkService.DidNotReceive().VerifyAndLink(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
