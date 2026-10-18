using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Invitations;

public class GameInvitationServiceShould : UnitTestBase
{
    private readonly IIntentionManager _intentionManager;
    private readonly IGameRepository _gameRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IGameInvitationRepository _repository;
    private readonly IGameSubscriptionService _subscriptionService;
    private readonly IUserBlacklistChecker _userBlacklistChecker;
    private readonly IEventProducer _producer;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly GameInvitationService _service;
    private readonly Guid _currentUserId;

    public GameInvitationServiceShould()
    {
        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _intentionManager = Mock<IIntentionManager>();

        _gameRepository = Mock<IGameRepository>();
        _userLookupService = Mock<IUserLookupService>();
        _repository = Mock<IGameInvitationRepository>();
        _subscriptionService = Mock<IGameSubscriptionService>();
        _userBlacklistChecker = Mock<IUserBlacklistChecker>();
        _userBlacklistChecker.IsBlockedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Create().Returns(Guid.NewGuid());

        _service = new GameInvitationService(
            _identityProvider,
            _intentionManager,
            _gameRepository,
            _userLookupService,
            _repository,
            _subscriptionService,
            _userBlacklistChecker,
            _producer,
            _dateTimeProvider,
            _guidFactory);
    }

    /// <summary>
    /// An invitation reaches its addressee signed with the inviter's name, so it
    /// is not created for somebody who keeps the inviter on their personal list.
    /// The inviter's own list is not asked: it restricts the people on it.
    /// </summary>
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public async Task AskTheInviteesPersonalListAndNotTheInviters(
        bool inviteeBlocksInviter, bool inviterBlocksInvitee, bool refused)
    {
        var gameId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = _currentUserId, Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameRepository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _userLookupService.GetAsync("invitee").Returns(new GeneralUser { UserId = inviteeId });
        _userBlacklistChecker.IsBlockedAsync(inviteeId, _currentUserId, Arg.Any<CancellationToken>())
            .Returns(inviteeBlocksInviter);
        _userBlacklistChecker.IsBlockedAsync(_currentUserId, inviteeId, Arg.Any<CancellationToken>())
            .Returns(inviterBlocksInvitee);
        _repository.InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameInvitationToken { TokenId = Guid.NewGuid() });
        _repository.GetInvitation(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((new GameInvitationToken(), new GameInvitation()));

        var act = async () => await _service.InvitePlayer(gameId, "invitee");

        if (refused)
        {
            await act.Should().ThrowAsync<HttpException>()
                .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.CannotInviteThisUser);
            await _repository.DidNotReceive()
                .InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>());
        }
        else
        {
            await act.Should().NotThrowAsync();
            await _repository.Received(1)
                .InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task AuthorizeInvitePlayerAction()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameRepository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = userId });
        _repository.InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameInvitationToken { TokenId = Guid.NewGuid() });
        _repository.GetInvitation(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((new GameInvitationToken(), new GameInvitation()));

        await _service.InvitePlayer(gameId, username);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.InvitePlayer, game);
    }

    /// <summary>
    /// The pending list is a panel of the settings page, so it follows the page
    /// rather than the roster: EditSettings admits the curating mentor, while
    /// InvitePlayer and CancelInvitation above keep the leads' own gates. Read
    /// and write parting company here is the point — the mentor sees who was
    /// invited and cannot invite or revoke.
    /// </summary>
    [Fact]
    public async Task AuthorizePendingInvitationsReadWithTheSettingsIntention()
    {
        var gameId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameRepository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _repository.GetPendingInvitations(gameId, Arg.Any<CancellationToken>()).Returns(Array.Empty<GameInvitation>());

        await _service.GetPendingInvitations(gameId);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.EditSettings, game);
        _intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.Edit, game);
    }

    [Fact]
    public async Task PublishEventWhenInvitingPlayer()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameRepository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = userId });
        _repository.InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameInvitationToken { TokenId = tokenId });
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>())
            .Returns((new GameInvitationToken(), new GameInvitation()));

        await _service.InvitePlayer(gameId, username);

        await _producer.Received(1).SendAsync(EventType.PlayerInvitationCreated, tokenId);
    }

    /// <summary>
    /// Inviting somebody the game blacklisted is the lead's own decision: the
    /// entry withdraws the invitations already sent, not the right to send one.
    /// </summary>
    [Fact]
    public async Task InviteAUserTheGameBlacklisted()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = new[] { new BlacklistedUser { UserId = userId } }
        };
        _gameRepository.GetGame(gameId, _currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        _userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = userId });
        _repository.InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameInvitationToken { TokenId = tokenId });
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>())
            .Returns((new GameInvitationToken(), new GameInvitation()));

        await _service.InviteReader(gameId, username);

        await _repository.Received(1).InvalidateAndCreateInvitation(
            Arg.Is<CreateGameInvitationEntity>(e => e.UserId == userId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptPlayerInvitationAndRemoveToken()
    {
        var tokenId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var token = new GameInvitationToken
        {
            TokenId = tokenId,
            UserId = _currentUserId,
            EntityId = gameId,
            TokenType = TokenType.GamePlayerInvitation,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>()).Returns((token, new GameInvitation()));
        _repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(1);

        await _service.AcceptInvitation(tokenId);

        await _repository.Received(1).RemoveInvitation(tokenId, Arg.Any<CancellationToken>());
        await _producer.Received(1).SendAsync(EventType.PlayerInvitationAccepted, tokenId);
    }

    [Fact]
    public async Task AcceptReaderInvitationAndSubscribe()
    {
        var tokenId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var token = new GameInvitationToken
        {
            TokenId = tokenId,
            UserId = _currentUserId,
            EntityId = gameId,
            TokenType = TokenType.GameReaderInvitation,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>()).Returns((token, new GameInvitation()));
        _repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(1);
        _subscriptionService.SubscribeAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new DM.Domain.Core.Subscriptions.Subscription());

        await _service.AcceptInvitation(tokenId);

        await _subscriptionService.Received(1).SubscribeAsync(gameId, Arg.Any<CancellationToken>());
        await _producer.Received(1).SendAsync(EventType.ReaderInvitationAccepted, tokenId);
    }

    /// <summary>
    /// The acceptance that spent no link announces nothing, whichever role it was for.
    /// </summary>
    /// <remarks>
    /// Both links are spent by a conditional statement carrying the soft-delete
    /// filter, so a link already spent - by the acceptance running beside this one,
    /// or by the master revoking it a moment earlier - matches nothing. The event
    /// carries the token, and announcing it from the call that spent nothing tells
    /// the game a seat was taken as many times as the link was opened, and mails
    /// the master about each of them.
    /// </remarks>
    [Theory]
    [InlineData(TokenType.GamePlayerInvitation, EventType.PlayerInvitationAccepted)]
    [InlineData(TokenType.GameReaderInvitation, EventType.ReaderInvitationAccepted)]
    public async Task StaySilentWhenTheInvitationWasAlreadySpentBesideThisAcceptance(
        TokenType tokenType, EventType announcement)
    {
        var tokenId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>()).Returns((
            new GameInvitationToken
            {
                TokenId = tokenId,
                UserId = _currentUserId,
                EntityId = gameId,
                TokenType = tokenType,
                CreatedUtc = DateTimeOffset.UtcNow
            },
            new GameInvitation()));
        _repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(0);
        _subscriptionService.SubscribeAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new DM.Domain.Core.Subscriptions.Subscription());

        await _service.AcceptInvitation(tokenId);

        await _repository.Received(1).RemoveInvitation(tokenId, Arg.Any<CancellationToken>());
        await _producer.DidNotReceive().SendAsync(announcement, Arg.Any<Guid>());
    }

    /// <summary>
    /// The rejection that spent no link announces nothing, whichever role it was
    /// for.
    /// </summary>
    /// <remarks>
    /// The same statement spends the link on this side, so the same race reaches
    /// it: two clicks on the refusal, or a master who cancelled the invitation a
    /// moment earlier. The event carries the token, and announcing it from the
    /// call that spent nothing mails the master a refusal for every time the
    /// letter was opened.
    /// </remarks>
    [Theory]
    [InlineData(TokenType.GamePlayerInvitation, EventType.PlayerInvitationRejected)]
    [InlineData(TokenType.GameReaderInvitation, EventType.ReaderInvitationRejected)]
    [InlineData(TokenType.GameAssistantInvitation, EventType.AssignmentRequestRejected)]
    public async Task StaySilentWhenTheInvitationWasAlreadySpentBesideThisRejection(
        TokenType tokenType, EventType announcement)
    {
        var tokenId = Invitation(tokenType);
        _repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(0);

        await _service.RejectInvitation(tokenId);

        await _repository.Received(1).RemoveInvitation(tokenId, Arg.Any<CancellationToken>());
        await _producer.DidNotReceive().SendAsync(announcement, Arg.Any<Guid>());
    }

    /// <summary>The rejection that spent the link is the one that announces it.</summary>
    [Theory]
    [InlineData(TokenType.GamePlayerInvitation, EventType.PlayerInvitationRejected)]
    [InlineData(TokenType.GameReaderInvitation, EventType.ReaderInvitationRejected)]
    [InlineData(TokenType.GameAssistantInvitation, EventType.AssignmentRequestRejected)]
    public async Task AnnounceTheRejectionThatSpentTheInvitation(
        TokenType tokenType, EventType announcement)
    {
        var tokenId = Invitation(tokenType);
        _repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(1);

        await _service.RejectInvitation(tokenId);

        await _producer.Received(1).SendAsync(announcement, tokenId);
    }

    /// <summary>A live invitation of any kind, addressed to the current user.</summary>
    private Guid Invitation(TokenType tokenType)
    {
        var tokenId = Guid.NewGuid();
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>()).Returns((
            new GameInvitationToken
            {
                TokenId = tokenId,
                UserId = _currentUserId,
                EntityId = Guid.NewGuid(),
                TokenType = tokenType,
                CreatedUtc = DateTimeOffset.UtcNow
            },
            new GameInvitation()));
        return tokenId;
    }

    [Fact]
    public async Task AnnounceTheAssistantSeatTheAcceptanceGranted()
    {
        var tokenId = AssistantInvitation();
        _repository.AcceptAssistantInvitation(
                Arg.Any<AddAssistantEntity>(), tokenId, Arg.Any<CancellationToken>())
            .Returns(true);

        await _service.AcceptInvitation(tokenId);

        await _producer.Received(1).SendAsync(EventType.AssignmentRequestAccepted, tokenId);
    }

    /// <summary>
    /// The acceptance that spent nothing announces nothing.
    /// </summary>
    /// <remarks>
    /// Two people following one link, or one person following it twice, both end
    /// with the same assistant row and only one of the two spent the invitation.
    /// The event carries the token, so announcing it from the second call tells
    /// the game a seat was taken twice and mails the master about it twice.
    /// </remarks>
    [Fact]
    public async Task StaySilentWhenTheInvitationWasSpentBesideThisAcceptance()
    {
        var tokenId = AssistantInvitation();
        _repository.AcceptAssistantInvitation(
                Arg.Any<AddAssistantEntity>(), tokenId, Arg.Any<CancellationToken>())
            .Returns(false);

        await _service.AcceptInvitation(tokenId);

        await _repository.Received(1).AcceptAssistantInvitation(
            Arg.Any<AddAssistantEntity>(), tokenId, Arg.Any<CancellationToken>());
        await _producer.DidNotReceive().SendAsync(EventType.AssignmentRequestAccepted, Arg.Any<Guid>());
    }

    /// <summary>A live assistant invitation addressed to the current user.</summary>
    private Guid AssistantInvitation()
    {
        var tokenId = Guid.NewGuid();
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>()).Returns((
            new GameInvitationToken
            {
                TokenId = tokenId,
                UserId = _currentUserId,
                EntityId = Guid.NewGuid(),
                TokenType = TokenType.GameAssistantInvitation,
                CreatedUtc = DateTimeOffset.UtcNow
            },
            new GameInvitation()));
        return tokenId;
    }

    [Fact]
    public async Task ThrowNotFoundWhenInvitationDoesNotExist()
    {
        var tokenId = Guid.NewGuid();
        _repository.GetInvitation(tokenId, Arg.Any<CancellationToken>())
            .Returns(((GameInvitationToken?)null, (GameInvitation?)null));

        var act = async () => await _service.AcceptInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }
}
