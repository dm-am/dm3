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
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBotLinkRepository _repository;
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
        _identityProvider.Current.Returns(identity);
        _dateTimeProvider.Now.Returns(_now);
        _guidFactory.Create().Returns(_tokenId);

        _service = new BotLinkService(
            _identityProvider,
            _guidFactory,
            _dateTimeProvider,
            _repository);
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
        _repository.RemoveExistingTokens(_currentUserId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repository.CreateLinkToken(Arg.Any<CreateToken>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await _service.GenerateLinkCode("telegram");

        await _repository.Received(1).RemoveExistingTokens(_currentUserId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTokenWithCorrectData()
    {
        CreateToken? capturedToken = null;
        _repository.CreateLinkToken(Arg.Any<CreateToken>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci =>
            {
                var t = ci.ArgAt<CreateToken>(0);
                capturedToken = t;
            });

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
        _repository.CreateLinkToken(Arg.Any<CreateToken>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

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
        _repository.FindValidToken("ABCDEF", Arg.Any<CancellationToken>()).Returns((Token?)null);

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
        _repository.FindValidToken("ABCDEF", Arg.Any<CancellationToken>()).Returns(token);
        _repository.MarkTokenUsed(_tokenId, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _repository.SetChannelId(_currentUserId, "telegram", "12345", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _repository.InitializeChannelPreferences(_currentUserId, "telegram", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _repository.GetUsername(_currentUserId, Arg.Any<CancellationToken>()).Returns("CurrentUser");

        var result = await _service.VerifyAndLink("ABCDEF", "telegram", "12345");

        result.Success.Should().BeTrue();
        result.Username.Should().Be("CurrentUser");
        await _repository.Received(1).MarkTokenUsed(_tokenId, Arg.Any<CancellationToken>());
        await _repository.Received(1).SetChannelId(_currentUserId, "telegram", "12345", Arg.Any<CancellationToken>());
        await _repository.Received(1).InitializeChannelPreferences(_currentUserId, "telegram", Arg.Any<CancellationToken>());
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
        _repository.SetChannelId(_currentUserId, "discord", null, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _repository.ClearChannelPreferences(_currentUserId, "discord", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await _service.Disconnect("discord");

        await _repository.Received(1).SetChannelId(_currentUserId, "discord", null, Arg.Any<CancellationToken>());
        await _repository.Received(1).ClearChannelPreferences(_currentUserId, "discord", Arg.Any<CancellationToken>());
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
