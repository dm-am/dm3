using System;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Tokens;

public class TokenFactoryShould : UnitTestBase
{
    private readonly TokenFactory _factory;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IDateTimeProvider> _dateTimeProvider;

    public TokenFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _factory = new TokenFactory(
            _guidFactory.Object,
            _dateTimeProvider.Object);
    }

    [Fact]
    public void CreateTokenWithoutEntityId()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _guidFactory.Setup(f => f.Create()).Returns(tokenId);
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var result = _factory.Create(userId, TokenType.Activation);

        result.Should().NotBeNull();
        result.TokenId.Should().Be(tokenId);
        result.UserId.Should().Be(userId);
        result.Type.Should().Be(TokenType.Activation);
        result.CreatedUtc.Should().Be(now);
        result.EntityId.Should().BeNull();
    }

    [Fact]
    public void CreateTokenWithEntityId()
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        _guidFactory.Setup(f => f.Create()).Returns(tokenId);
        _dateTimeProvider.Setup(d => d.Now).Returns(now);

        var result = _factory.Create(userId, entityId, TokenType.EmailChange);

        result.Should().NotBeNull();
        result.TokenId.Should().Be(tokenId);
        result.UserId.Should().Be(userId);
        result.EntityId.Should().Be(entityId);
        result.Type.Should().Be(TokenType.EmailChange);
        result.CreatedUtc.Should().Be(now);
    }

    [Fact]
    public void CreatePasswordResetToken()
    {
        var userId = Guid.NewGuid();
        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(userId, TokenType.PasswordChange);

        result.Type.Should().Be(TokenType.PasswordChange);
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public void CreateActivationToken()
    {
        var userId = Guid.NewGuid();
        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());
        _dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(userId, TokenType.Activation);

        result.Type.Should().Be(TokenType.Activation);
        result.UserId.Should().Be(userId);
    }
}
