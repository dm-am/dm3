using System;
using System.Globalization;
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
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Xunit;
using BotLinkResult = DM.Domain.Personal.Features.Notifications.BotLinkResult;

namespace DM.Web.API.Tests.Features.Personal;

/// <summary>
/// The Discord branch checks an Ed25519 signature over timestamp and raw body: no key closes the endpoint,
/// a bad signature answers 401, which Discord's validation requires, and so does a signed timestamp far from
/// the server's clock. Telegram keeps its secret header and 403.
/// </summary>
public class WebhookControllerShould : UnitTestBase
{
    // A key pair playing the Discord application: the test signs the way Discord does and
    // configures the controller with the public half.
    private static readonly Ed25519PrivateKeyParameters PrivateKey = new(new SecureRandom());

    private static readonly string PublicKeyHex =
        Convert.ToHexString(PrivateKey.GeneratePublicKey().GetEncoded());

    // The moment the default timestamp of SignedHeaders names.
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000000);

    private readonly IBotLinkService _botLinkService;

    public WebhookControllerShould()
    {
        _botLinkService = Mock<IBotLinkService>();
        _botLinkService
            .VerifyAndLink(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new BotLinkResult { Success = true, Username = "CurrentUser" });
    }

    private WebhookController Controller(
        BotConfiguration config, string body, params (string Name, string Value)[] headers)
    {
        var controller = new WebhookController(
            new IWebhookHandler[]
            {
                new DiscordWebhookHandler(_botLinkService, Mock<ILogger<DiscordWebhookHandler>>()),
                new TelegramWebhookHandler(_botLinkService, Mock<ILogger<TelegramWebhookHandler>>()),
            },
            Options.Create(config),
            new DiscordSignatureVerifier(new FixedClock(Now), Mock<ILogger<DiscordSignatureVerifier>>()),
            Mock<ILogger<WebhookController>>());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        foreach (var (name, value) in headers)
        {
            httpContext.Request.Headers[name] = value;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    // Signs the way Discord does: Ed25519 over UTF-8 of timestamp + raw body.
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

    private static string SecondsFromNow(int seconds) =>
        (Now.ToUnixTimeSeconds() + seconds).ToString(CultureInfo.InvariantCulture);

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

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    [InlineData(-86400)]
    public async Task RefuseACorrectlySignedInteractionFarFromTheClockWith401(int secondsFromNow)
    {
        var body = """{"type": 1}""";
        var controller = Controller(DiscordConfigured(), body, SignedHeaders(body, SecondsFromNow(secondsFromNow)));

        var call = () => controller.HandleWebhook("discord");

        (await call.Should().ThrowAsync<HttpException>("a captured interaction stays validly signed for ever"))
            .Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(-300)]
    [InlineData(300)]
    public async Task AcceptASignedInteractionAClockSkewAway(int secondsFromNow)
    {
        var body = """{"type": 1}""";
        var controller = Controller(DiscordConfigured(), body, SignedHeaders(body, SecondsFromNow(secondsFromNow)));

        var result = await controller.HandleWebhook("discord");

        result.Should().BeOfType<OkObjectResult>("two clocks minutes apart still name the same moment");
    }

    [Theory]
    [InlineData("yesterday")]
    [InlineData("-1700000000")]
    [InlineData("1700000000.5")]
    [InlineData(" 1700000000")]
    [InlineData("99999999999999999999999999")]
    public async Task RefuseASignedTimestampThatIsNotUnixSecondsWith401(string timestamp)
    {
        var body = """{"type": 1}""";
        var controller = Controller(DiscordConfigured(), body, SignedHeaders(body, timestamp));

        var call = () => controller.HandleWebhook("discord");

        (await call.Should().ThrowAsync<HttpException>("a moment that cannot be read cannot be shown to be recent"))
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
    public async Task AnswerAnUnknownTypeAsAnUnconfiguredOne()
    {
        var controller = Controller(
            new BotConfiguration { TelegramWebhookSecret = "the-secret", DiscordPublicKey = PublicKeyHex }, "{}",
            ("X-Telegram-Bot-Api-Secret-Token", "the-secret"));

        var result = await controller.HandleWebhook("slack");

        result.Should().BeOfType<NotFoundResult>("a probe of the path learns nothing from the answer");
    }

    [Fact]
    public async Task RefuseAWrongSecretBeforeTheBodyIsParsed()
    {
        var controller = Controller(
            new BotConfiguration { TelegramWebhookSecret = "the-secret" }, "not json",
            ("X-Telegram-Bot-Api-Secret-Token", "not-the-secret"));

        var call = () => controller.HandleWebhook("telegram");

        (await call.Should().ThrowAsync<HttpException>("a caller who failed the check is told nothing about the body"))
            .Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        await _botLinkService.Received(1).VerifyAndLink("ABC123", "telegram", "12345", Arg.Any<CancellationToken>());
    }
}
