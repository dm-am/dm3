using System;
using DM.Services.Common.BusinessProcesses.Tokens;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using DM.Tests.Core;
using FluentAssertions;
using Moq.Language.Flow;
using Xunit;

namespace DM.Services.Community.Tests.BusinessProcesses;

public class TokenFactoryShould : UnitTestBase
{
    private readonly ISetup<IGuidFactory, Guid> newIdSetup;
    private readonly ISetup<IDateTimeProvider, DateTimeOffset> currentMomentSetup;
    private readonly TokenFactory factory;

    public TokenFactoryShould()
    {
        var guidFactory = Mock<IGuidFactory>();
        newIdSetup = guidFactory.Setup(f => f.Create());

        var dateTimeProvider = Mock<IDateTimeProvider>();
        currentMomentSetup = dateTimeProvider.Setup(p => p.Now);

        factory = new TokenFactory(guidFactory.Object, dateTimeProvider.Object);
    }

    [Fact]
    public void CreateTokenWithUserIdOnly()
    {
        var dateTimeOffset = new DateTimeOffset(2019, 05, 02, 16, 10, 10, TimeSpan.Zero);
        currentMomentSetup.Returns(dateTimeOffset);
        var tokenId = Guid.NewGuid();
        newIdSetup.Returns(tokenId);

        var userId = Guid.NewGuid();
        var actual = factory.Create(userId, TokenType.Activation);

        actual.Should().BeEquivalentTo(new Token
        {
            TokenId = tokenId,
            UserId = userId,
            EntityId = null,
            Type = TokenType.Activation,
            CreatedUtc = dateTimeOffset,
            IsRemoved = false
        });
    }

    [Fact]
    public void CreateTokenWithEntityId()
    {
        var dateTimeOffset = new DateTimeOffset(2019, 05, 02, 16, 10, 10, TimeSpan.Zero);
        currentMomentSetup.Returns(dateTimeOffset);
        var tokenId = Guid.NewGuid();
        newIdSetup.Returns(tokenId);

        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var actual = factory.Create(userId, entityId, TokenType.PlayerInvitation);

        actual.Should().BeEquivalentTo(new Token
        {
            TokenId = tokenId,
            UserId = userId,
            EntityId = entityId,
            Type = TokenType.PlayerInvitation,
            CreatedUtc = dateTimeOffset,
            IsRemoved = false
        });
    }
}