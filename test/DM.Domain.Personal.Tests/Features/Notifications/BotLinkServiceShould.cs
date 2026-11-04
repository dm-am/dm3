using DM.Domain.Core.Exceptions;
using System.Net;
using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Tokens;
using DM.Domain.Personal.Features.Notifications;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

public class BotLinkServiceShould : UnitTestBase
{
    private readonly IIdentityProvider identityProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IBotLinkRepository repository;
    private readonly BotLinkService service;
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly Guid tokenId = Guid.NewGuid();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    public BotLinkServiceShould()
    {
        identityProvider = Mock<IIdentityProvider>();
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        repository = Mock<IBotLinkRepository>();

        var identity = Identities.User(currentUserId, "CurrentUser", UserRole.RegularUser);
        identityProvider.Current.Returns(identity);
        dateTimeProvider.Now.Returns(now);
        guidFactory.Create().Returns(tokenId);

        service = new BotLinkService(
            identityProvider,
            guidFactory,
            dateTimeProvider,
            repository);
    }

    [Fact]
    public async Task ThrowWhenGeneratingLinkCodeWithInvalidChannelType()
    {
        var act = () => service.GenerateLinkCode("invalid");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .Where(e => e.Message.Contains("Неизвестный канал"));
    }

    [Fact]
    public async Task RemoveExistingTokensWhenGeneratingNewCode()
    {
        repository.RemoveExistingTokens(currentUserId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        repository.CreateLinkToken(Arg.Any<CreateToken>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await service.GenerateLinkCode("telegram");

        await repository.Received(1).RemoveExistingTokens(currentUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTokenWithCorrectData()
    {
        CreateToken? capturedToken = null;
        repository.CreateLinkToken(Arg.Do<CreateToken>(t => capturedToken = t), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await service.GenerateLinkCode("discord");

        capturedToken.Should().NotBeNull();
        capturedToken!.TokenId.Should().Be(tokenId);
        capturedToken.UserId.Should().Be(currentUserId);
        capturedToken.Type.Should().Be(TokenType.NotificationBotLink);
        capturedToken.CreatedUtc.Should().Be(now);
    }

    [Fact]
    public async Task GenerateASixCharacterCodeForTenMinutes()
    {
        var result = await service.GenerateLinkCode("telegram");

        result.Code.Should().HaveLength(6);
        result.ExpiresUtc.Should().BeCloseTo(now.AddMinutes(10), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ReturnErrorWhenVerifyingInvalidCodeFormat()
    {
        var result = await service.VerifyAndLink("abc", "telegram", "12345");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Неверный формат кода");
    }

    [Fact]
    public async Task ReturnErrorWhenVerifyingExpiredCode()
    {
        repository.FindValidToken("ABCDEF", Arg.Any<CancellationToken>()).Returns((Token?)null);

        var result = await service.VerifyAndLink("ABCDEF", "telegram", "12345");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Код неверный или истек");
    }

    [Fact]
    public async Task VerifyAndLinkSuccessfully()
    {
        var token = new Token
        {
            TokenId = tokenId,
            UserId = currentUserId,
            Type = TokenType.NotificationBotLink
        };
        repository.FindValidToken("ABCDEF", Arg.Any<CancellationToken>()).Returns(token);
        repository.MarkTokenUsed(tokenId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        repository.SetChannelId(currentUserId, "telegram", "12345", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        repository.InitializeChannelPreferences(currentUserId, "telegram", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        repository.GetUsername(currentUserId, Arg.Any<CancellationToken>()).Returns("CurrentUser");

        var result = await service.VerifyAndLink("ABCDEF", "telegram", "12345");

        result.Success.Should().BeTrue();
        result.Username.Should().Be("CurrentUser");
        await repository.Received(1).MarkTokenUsed(tokenId, Arg.Any<CancellationToken>());
        await repository.Received(1).SetChannelId(currentUserId, "telegram", "12345", Arg.Any<CancellationToken>());
        await repository.Received(1).InitializeChannelPreferences(currentUserId, "telegram", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ThrowWhenDisconnectingInvalidChannelType()
    {
        var act = () => service.Disconnect("invalid");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .Where(e => e.Message.Contains("Неизвестный канал"));
    }

    [Fact]
    public async Task DisconnectChannelSuccessfully()
    {
        repository.SetChannelId(currentUserId, "discord", null, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        repository.ClearChannelPreferences(currentUserId, "discord", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await service.Disconnect("discord");

        await repository.Received(1).SetChannelId(currentUserId, "discord", null, Arg.Any<CancellationToken>());
        await repository.Received(1).ClearChannelPreferences(currentUserId, "discord", Arg.Any<CancellationToken>());
    }
}
