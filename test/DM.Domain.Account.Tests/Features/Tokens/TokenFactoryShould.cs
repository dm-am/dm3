using System;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Tokens;

public class TokenFactoryShould : UnitTestBase
{
    private readonly TokenFactory _factory;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TokenFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _factory = new TokenFactory(
            _guidFactory,
            _dateTimeProvider);
    }

    [Fact]
    public void CreateTokenWithoutEntityId()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _guidFactory.Create().Returns(tokenId);
        _dateTimeProvider.Now.Returns(now);

        var result = _factory.Create(userId, TokenType.Activation);

        result.Should().NotBeNull();
        result.TokenId.Should().Be(tokenId);
        result.UserId.Should().Be(userId);
        result.Type.Should().Be(TokenType.Activation);
        result.CreatedUtc.Should().Be(now);
        result.EntityId.Should().BeNull();
    }

    [Fact]
    public void CreatePasswordResetToken()
    {
        var userId = Guid.NewGuid();
        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(userId, TokenType.PasswordChange);

        result.Type.Should().Be(TokenType.PasswordChange);
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public void CreateActivationToken()
    {
        var userId = Guid.NewGuid();
        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(userId, TokenType.Activation);

        result.Type.Should().Be(TokenType.Activation);
        result.UserId.Should().Be(userId);
    }
}
