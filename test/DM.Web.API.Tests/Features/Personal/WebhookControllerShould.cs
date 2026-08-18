using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Core.Configuration;
using DM.Testing;
using DM.Web.API.Features.Personal.Notifications;
using DM.Web.API.Features.Personal.Webhooks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Xunit;
using BotLinkResult = DM.Domain.Personal.Features.Notifications.BotLinkResult;

namespace DM.Web.API.Tests.Features.Personal;

/// <summary>
/// The Discord branch of the webhook speaks the official interactions
/// contract: no shared secret, an Ed25519 signature over timestamp + raw
/// body instead. Two properties are load-bearing. An unconfigured public key
/// closes the endpoint rather than opening it, and a bad signature answers
/// 401 — Discord validates the endpoint by deliberately sending broken
/// signatures and requires exactly that code, so a 403 here fails the
/// registration. The Telegram branch keeps its secret-header contract and
/// its 403 untouched.
/// </summary>
public class WebhookControllerShould : UnitTestBase
{
    /// <summary>
    /// A key pair playing the Discord application: the test signs the way
    /// Discord does and configures the controller with the public half.
    /// </summary>
    private static readonly Ed25519PrivateKeyParameters PrivateKey = new(new SecureRandom());

    private static readonly string PublicKeyHex =
        Convert.ToHexString(PrivateKey.GeneratePublicKey().GetEncoded());

    private readonly Mock<IBotLinkService> _botLinkService;

    public WebhookControllerShould()
    {
        _botLinkService = Mock<IBotLinkService>();
        _botLinkService
            .Setup(s => s.VerifyAndLink(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BotLinkResult { Success = true, Username = "CurrentUser" });
    }

    private WebhookController Controller(
        BotConfiguration config, string body, params (string Name, string Value)[] headers)
    {
        var controller = new WebhookController(
            new IWebhookHandler[]
            {
                new DiscordWebhookHandler(_botLinkService.Object, Mock<ILogger<DiscordWebhookHandler>>().Object),
                new TelegramWebhookHandler(_botLinkService.Object, Mock<ILogger<TelegramWebhookHandler>>().Object),
            },
            Options.Create(config),
            Mock<ILogger<WebhookController>>().Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        foreach (var (name, value) in headers)
        {
            httpContext.Request.Headers[name] = value;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    /// <summary>Signs the way Discord does: Ed25519 over UTF-8 of timestamp + raw body.</summary>
    private static string Sign(string timestamp, string body)
    {
        var signer = new Ed25519Signer();
        signer.Init(true, PrivateKey);
        var message = Encoding.UTF8.GetBytes(timestamp + body);
        signer.BlockUpdate(message, 0, message.Length);
        return Convert.ToHexString(signer.GenerateSignature());
    }

    private static (string, string)[] SignedHeaders(string body, string timestamp = "1700000000") =>
    [
        ("X-Signature-Ed25519", Sign(timestamp, body)),
        ("X-Signature-Timestamp", timestamp),
    ];

    private static BotConfiguration DiscordConfigured() => new() { DiscordPublicKey = PublicKeyHex };

    [Fact]
    public async Task CloseTheDiscordEndpointWhenNoPublicKeyIsConfigured()
    {
        var body = """{"type": 1}""";
        var controller = Controller(new BotConfiguration(), body, SignedHeaders(body));

        var result = await controller.HandleWebhook("discord");

        // An unconfigured key is the shipped default: the endpoint must not
        // exist rather than accept anything.
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RefuseAMissingSignatureWith401()
    {
        var controller = Controller(DiscordConfigured(), """{"type": 1}""");

        var call = () => controller.HandleWebhook("discord");

        (await call.Should().ThrowAsync<HttpException>())
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefuseAWrongSignatureWith401()
    {
        var body = """{"type": 1}""";
        var controller = Controller(DiscordConfigured(), body,
            ("X-Signature-Ed25519", Sign("1700000000", """{"type": 2}""")),
            ("X-Signature-Timestamp", "1700000000"));

        var call = () => controller.HandleWebhook("discord");

        (await call.Should().ThrowAsync<HttpException>("the signature covers other bytes than these"))
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefuseASignatureThatIsNotHexWith401()
    {
        var controller = Controller(DiscordConfigured(), """{"type": 1}""",
            ("X-Signature-Ed25519", "not-hex-at-all"),
            ("X-Signature-Timestamp", "1700000000"));

        var call = () => controller.HandleWebhook("discord");

        (await call.Should().ThrowAsync<HttpException>("malformed input from the internet is a refusal, not a 500"))
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefuseASignatureOverAnotherTimestampWith401()
    {
        var body = """{"type": 1}""";
        var controller = Controller(DiscordConfigured(), body,
            ("X-Signature-Ed25519", Sign("1700000000", body)),
            ("X-Signature-Timestamp", "1700000001"));

        var call = () => controller.HandleWebhook("discord");

        (await call.Should().ThrowAsync<HttpException>("the timestamp is under the signature, so a substituted one fails"))
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AnswerPongToACorrectlySignedPing()
    {
        var body = """{"type": 1}""";
        var controller = Controller(DiscordConfigured(), body, SignedHeaders(body));

        var result = await controller.HandleWebhook("discord");

        var response = result.Should().BeOfType<OkObjectResult>().Which.Value;
        JsonSerializer.SerializeToElement(response!).GetProperty("type").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task DispatchASignedConnectCommand()
    {
        var body = """
            {"type": 2, "data": {"name": "connect", "options": [{"name": "code", "type": 3, "value": "ABC123"}]}, "member": {"user": {"id": "9876543210"}}}
            """;
        var controller = Controller(DiscordConfigured(), body, SignedHeaders(body));

        var result = await controller.HandleWebhook("discord");

        _botLinkService.Verify(
            s => s.VerifyAndLink("ABC123", "discord", "9876543210", It.IsAny<CancellationToken>()), Times.Once);
        var response = result.Should().BeOfType<OkObjectResult>().Which.Value;
        var json = JsonSerializer.SerializeToElement(response!);
        json.GetProperty("type").GetInt32().Should().Be(4);
        json.GetProperty("data").GetProperty("flags").GetInt32().Should().Be(64);
    }

    [Fact]
    public async Task KeepTheTelegramSecretContractClosedWhenUnconfigured()
    {
        var controller = Controller(new BotConfiguration(), "{}");

        var result = await controller.HandleWebhook("telegram");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task KeepTheTelegramRefusalAt403()
    {
        var controller = Controller(
            new BotConfiguration { TelegramWebhookSecret = "the-secret" }, "{}",
            ("X-Telegram-Bot-Api-Secret-Token", "not-the-secret"));

        var call = () => controller.HandleWebhook("telegram");

        (await call.Should().ThrowAsync<HttpException>("Telegram keeps its own contract untouched"))
            .Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DispatchATelegramUpdateCarryingTheRightSecret()
    {
        var controller = Controller(
            new BotConfiguration { TelegramWebhookSecret = "the-secret" },
            """{"message": {"text": "/connect ABC123", "chat": {"id": 12345}}}""",
            ("X-Telegram-Bot-Api-Secret-Token", "the-secret"));

        var result = await controller.HandleWebhook("telegram");

        result.Should().BeOfType<OkResult>("Telegram reads nothing from the response body");
        _botLinkService.Verify(
            s => s.VerifyAndLink("ABC123", "telegram", "12345", It.IsAny<CancellationToken>()), Times.Once);
    }
}
