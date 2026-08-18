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

/// <summary>
/// The Discord handler speaks the official interactions contract: PING is
/// answered with PONG, a slash command with a type 4 interaction response,
/// and the /connect reply is ephemeral — a linking code is the invoker's
/// business and nobody else's.
/// </summary>
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

    /// <summary>
    /// The response as Discord will read it. Serialized through the property
    /// name attributes the DTOs pin, so a rename that breaks the wire contract
    /// breaks this too.
    /// </summary>
    private static JsonElement AsWireJson(object response) =>
        JsonSerializer.SerializeToElement(response);

    private const string ConnectInGuild = """
        {
            "type": 2,
            "data": {"name": "connect", "options": [{"name": "code", "type": 3, "value": "ABC123"}]},
            "member": {"user": {"id": "9876543210"}}
        }
        """;

    [Fact]
    public void HaveDiscordBotType()
    {
        _handler.BotType.Should().Be("discord");
    }

    [Fact]
    public async Task AnswerPongToPing()
    {
        var response = await _handler.HandleAsync(Payload("""{"type": 1}"""));

        response.Should().NotBeNull();
        var json = AsWireJson(response!);
        json.GetProperty("type").GetInt32().Should().Be(1);
        json.TryGetProperty("data", out _).Should().BeFalse(
            "PONG carries no data, and a null one would be noise in the contract");
    }

    [Fact]
    public async Task VerifyAndLinkOnConnectCommandFromAGuild()
    {
        var response = await _handler.HandleAsync(Payload(ConnectInGuild));

        _botLinkService.Verify(s => s.VerifyAndLink("ABC123", "discord", "9876543210", It.IsAny<CancellationToken>()), Times.Once);

        var json = AsWireJson(response!);
        json.GetProperty("type").GetInt32().Should().Be(4);
        json.GetProperty("data").GetProperty("flags").GetInt32().Should().Be(64,
            "the reply is ephemeral: only the invoker sees the result of their own code");
        json.GetProperty("data").GetProperty("content").GetString().Should().Contain("CurrentUser");
    }

    [Fact]
    public async Task VerifyAndLinkOnConnectCommandFromADirectMessage()
    {
        var payload = Payload("""
            {
                "type": 2,
                "data": {"name": "connect", "options": [{"name": "code", "type": 3, "value": "ABC123"}]},
                "user": {"id": "1112223334"}
            }
            """);

        await _handler.HandleAsync(payload);

        _botLinkService.Verify(s => s.VerifyAndLink("ABC123", "discord", "1112223334", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnswerEphemeralRefusalWhenTheCodeDoesNotVerify()
    {
        _botLinkService
            .Setup(s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotLinkResult { Success = false, Error = "Invalid or expired code" });

        var response = await _handler.HandleAsync(Payload(ConnectInGuild));

        var json = AsWireJson(response!);
        json.GetProperty("type").GetInt32().Should().Be(4,
            "every slash command gets an interaction response, refusals included");
        json.GetProperty("data").GetProperty("flags").GetInt32().Should().Be(64);
    }

    [Fact]
    public async Task AnswerAnUnknownCommandWithoutLinking()
    {
        var payload = Payload("""
            {
                "type": 2,
                "data": {"name": "weather"},
                "member": {"user": {"id": "9876543210"}}
            }
            """);

        var response = await _handler.HandleAsync(payload);

        _botLinkService.Verify(
            s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        AsWireJson(response!).GetProperty("type").GetInt32().Should().Be(4,
            "an unanswered command shows the invoker a failure in Discord's wording");
    }

    [Fact]
    public async Task NotLinkAConnectCommandWithoutAnInvoker()
    {
        var payload = Payload("""
            {
                "type": 2,
                "data": {"name": "connect", "options": [{"name": "code", "type": 3, "value": "ABC123"}]}
            }
            """);

        var response = await _handler.HandleAsync(payload);

        _botLinkService.Verify(
            s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        AsWireJson(response!).GetProperty("type").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task IgnoreInteractionTypesTheApplicationNeverRegisters()
    {
        var response = await _handler.HandleAsync(Payload("""{"type": 3, "data": {"custom_id": "x"}}"""));

        response.Should().BeNull("only PING and slash commands can arrive for this application");
    }
}
