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
    private readonly TokenFactory factory;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;

    public TokenFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        factory = new TokenFactory(
            guidFactory,
            dateTimeProvider);
    }

    [Theory]
    [InlineData(TokenType.PasswordChange)]
    public void CreateTokenWithoutEntityId(TokenType type)
    {
        var tokenId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        guidFactory.Create().Returns(tokenId);
        dateTimeProvider.Now.Returns(now);

        var result = factory.Create(userId, type);

        result.TokenId.Should().Be(tokenId);
        result.UserId.Should().Be(userId);
        result.Type.Should().Be(type);
        result.CreatedUtc.Should().Be(now);
        result.EntityId.Should().BeNull();
    }
}
