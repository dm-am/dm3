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
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

public class BotLinkServiceShould : UnitTestBase
{
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;
    private readonly Mock<IBotLinkRepository> _repository;
    private readonly BotLinkService _service;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Guid _tokenId = Guid.NewGuid();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public BotLinkServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _repository = Mock<IBotLinkRepository>();

        var identity = Identities.User(_currentUserId, "CurrentUser", UserRole.RegularUser);
        _identityProvider.Setup(p => p.Current).Returns(identity);
        _dateTimeProvider.Setup(d => d.Now).Returns(_now);
        _guidFactory.Setup(g => g.Create()).Returns(_tokenId);

        _service = new BotLinkService(
            _identityProvider.Object,
            _guidFactory.Object,
            _dateTimeProvider.Object,
            _repository.Object);
    }

    [Fact]
    public async Task ThrowWhenGeneratingLinkCodeWithInvalidChannelType()
    {
        var act = () => _service.GenerateLinkCode("invalid");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .Where(e => e.Message.Contains("Неизвестный канал"));
    }

    [Fact]
    public async Task RemoveExistingTokensWhenGeneratingNewCode()
    {
        _repository.Setup(r => r.RemoveExistingTokens(_currentUserId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.CreateLinkToken(It.IsAny<CreateToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.GenerateLinkCode("telegram");

        _repository.Verify(r => r.RemoveExistingTokens(_currentUserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTokenWithCorrectData()
    {
        CreateToken? capturedToken = null;
        _repository.Setup(r => r.CreateLinkToken(It.IsAny<CreateToken>(), It.IsAny<CancellationToken>()))
            .Callback<CreateToken, CancellationToken>((t, _) => capturedToken = t)
            .Returns(Task.CompletedTask);

        await _service.GenerateLinkCode("discord");

        capturedToken.Should().NotBeNull();
        capturedToken!.TokenId.Should().Be(_tokenId);
        capturedToken.UserId.Should().Be(_currentUserId);
        capturedToken.Type.Should().Be(TokenType.NotificationBotLink);
        capturedToken.CreatedUtc.Should().Be(_now);
    }

    [Fact]
    public async Task GenerateCodeFromFirst6CharsOfGuid()
    {
        _repository.Setup(r => r.CreateLinkToken(It.IsAny<CreateToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _service.GenerateLinkCode("telegram");

        result.Code.Should().HaveLength(6);
        result.Code.Should().Be(_tokenId.ToString()[..6].ToUpperInvariant());
        result.ExpiresUtc.Should().BeCloseTo(_now.AddMinutes(10), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ReturnErrorWhenVerifyingInvalidCodeFormat()
    {
        var result = await _service.VerifyAndLink("abc", "telegram", "12345");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid code format");
    }

    [Fact]
    public async Task ReturnErrorWhenVerifyingExpiredCode()
    {
        _repository.Setup(r => r.FindValidToken("ABCDEF", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Token?)null);

        var result = await _service.VerifyAndLink("ABCDEF", "telegram", "12345");

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid or expired code");
    }

    [Fact]
    public async Task VerifyAndLinkSuccessfully()
    {
        var token = new Token
        {
            TokenId = _tokenId,
            UserId = _currentUserId,
            Type = TokenType.NotificationBotLink
        };
        _repository.Setup(r => r.FindValidToken("ABCDEF", It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);
        _repository.Setup(r => r.MarkTokenUsed(_tokenId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.SetChannelId(_currentUserId, "telegram", "12345", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.InitializeChannelPreferences(_currentUserId, "telegram", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.GetUsername(_currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("CurrentUser");

        var result = await _service.VerifyAndLink("ABCDEF", "telegram", "12345");

        result.Success.Should().BeTrue();
        result.Username.Should().Be("CurrentUser");
        _repository.Verify(r => r.MarkTokenUsed(_tokenId, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.SetChannelId(_currentUserId, "telegram", "12345", It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.InitializeChannelPreferences(_currentUserId, "telegram", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenDisconnectingInvalidChannelType()
    {
        var act = () => _service.Disconnect("invalid");

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .Where(e => e.Message.Contains("Неизвестный канал"));
    }

    [Fact]
    public async Task DisconnectChannelSuccessfully()
    {
        _repository.Setup(r => r.SetChannelId(_currentUserId, "discord", null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.ClearChannelPreferences(_currentUserId, "discord", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _service.Disconnect("discord");

        _repository.Verify(r => r.SetChannelId(_currentUserId, "discord", null, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.ClearChannelPreferences(_currentUserId, "discord", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AcceptTelegramAsValidChannelType()
    {
        var act = () => _service.GenerateLinkCode("telegram");

        act.Should().NotThrowAsync();
    }

    [Fact]
    public void AcceptDiscordAsValidChannelType()
    {
        var act = () => _service.GenerateLinkCode("discord");

        act.Should().NotThrowAsync();
    }
}
