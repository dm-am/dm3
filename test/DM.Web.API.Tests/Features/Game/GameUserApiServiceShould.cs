using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing;
using DM.Testing.Dsl;
using DM.Web.API.Features.Game.Users;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using DomainGame = DM.Domain.Game.Features.Games.Game;

namespace DM.Web.API.Tests.Features.Game;

/// <summary>
/// The reader's button subscribes only to a game the reader may open.
/// </summary>
/// <remarks>The route takes a GUID without the visibility filter, so the service asks the game first.</remarks>
public class GameUserApiServiceShould : UnitTestBase
{
    private readonly IGameSubscriptionService _subscriptionService = Mock<IGameSubscriptionService>();
    private readonly IGameService _gameService = Mock<IGameService>();
    private readonly IIdentityProvider _identityProvider = Mock<IIdentityProvider>();
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly GameUserApiService _service;

    public GameUserApiServiceShould()
    {
        _identityProvider.Current.Returns(Identities.User(_currentUserId, "reader"));
        _service = new GameUserApiService(
            Mock<IGameInvitationService>(), _subscriptionService, _gameService, _identityProvider);
    }

    [Fact]
    public async Task RefuseToSubscribeToAGameHiddenFromTheReaderAsIfThereWereNone()
    {
        var gameId = Guid.NewGuid();
        _gameService.GetAsync(gameId)
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound));

        var act = async () => await _service.Subscribe(gameId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound && e.Message == RefusalMessage.GameNotFound);
        await _subscriptionService.DidNotReceive().SubscribeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubscribeToAGameTheReaderSees()
    {
        var gameId = Guid.NewGuid();
        _gameService.GetAsync(gameId).Returns(new DomainGame { Id = gameId });
        _subscriptionService.GetSubscribersAsync(gameId, Arg.Any<CancellationToken>())
            .Returns([new UserReference { UserId = _currentUserId, Username = "reader" }]);

        var reader = await _service.Subscribe(gameId);

        reader.User.Username.Should().Be("reader");
        await _subscriptionService.Received(1).SubscribeAsync(gameId, Arg.Any<CancellationToken>());
    }
}
