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
using DM.Domain.Core.Subscriptions;
using DM.Domain.Core.Users;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Blacklists;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Invitations;

public class GameInvitationServiceShould : UnitTestBase
{
    private readonly IIntentionManager intentionManager;
    private readonly IGameRepository gameRepository;
    private readonly IUserLookupService userLookupService;
    private readonly IGameInvitationRepository repository;
    private readonly IGameSubscriptionService subscriptionService;
    private readonly IUserBlacklistChecker userBlacklistChecker;
    private readonly IEventProducer producer;
    private readonly IIdentityProvider identityProvider;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly GameInvitationService service;
    private readonly Guid currentUserId;

    public GameInvitationServiceShould()
    {
        currentUserId = Guid.NewGuid();
        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId, UserRole.RegularUser));

        intentionManager = Mock<IIntentionManager>();

        gameRepository = Mock<IGameRepository>();
        userLookupService = Mock<IUserLookupService>();
        repository = Mock<IGameInvitationRepository>();
        subscriptionService = Mock<IGameSubscriptionService>();
        userBlacklistChecker = Mock<IUserBlacklistChecker>();
        userBlacklistChecker.IsBlockedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        producer = Mock<IEventProducer>();
        producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        service = new GameInvitationService(
            identityProvider,
            intentionManager,
            gameRepository,
            userLookupService,
            repository,
            subscriptionService,
            userBlacklistChecker,
            producer,
            dateTimeProvider,
            guidFactory);
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
            Master = new GeneralUser { UserId = currentUserId, Username = "Author" }
        };
        gameRepository.GetGame(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        userLookupService.GetAsync("invitee").Returns(new GeneralUser { UserId = inviteeId });
        userBlacklistChecker.IsBlockedAsync(inviteeId, currentUserId, Arg.Any<CancellationToken>())
            .Returns(inviteeBlocksInviter);
        userBlacklistChecker.IsBlockedAsync(currentUserId, inviteeId, Arg.Any<CancellationToken>())
            .Returns(inviterBlocksInvitee);
        repository.InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameInvitationToken { TokenId = Guid.NewGuid() });
        repository.GetInvitation(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((new GameInvitationToken(), new GameInvitation()));

        var act = async () => await service.InvitePlayer(gameId, "invitee");

        if (refused)
        {
            await act.Should().ThrowAsync<HttpException>()
                .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.CannotInviteThisUser);
            await repository.DidNotReceive()
                .InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>());
        }
        else
        {
            await act.Should().NotThrowAsync();
            await repository.Received(1)
                .InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task AuthorizeInvitePlayerAction()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var game = GameBuilder.Bare(gameId);
        gameRepository.GetGame(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = userId });
        repository.InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameInvitationToken { TokenId = Guid.NewGuid() });
        repository.GetInvitation(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((new GameInvitationToken(), new GameInvitation()));

        await service.InvitePlayer(gameId, username);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.InvitePlayer, game);
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
        var game = GameBuilder.Bare(gameId);
        gameRepository.GetGame(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        repository.GetPendingInvitations(gameId, Arg.Any<CancellationToken>()).Returns(Array.Empty<GameInvitation>());

        await service.GetPendingInvitations(gameId);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.EditSettings, game);
        intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.Edit, game);
    }

    [Fact]
    public async Task PublishEventWhenInvitingPlayer()
    {
        var gameId = Guid.NewGuid();
        var username = "testuser";
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var game = GameBuilder.Bare(gameId);
        gameRepository.GetGame(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = userId });
        repository.InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameInvitationToken { TokenId = tokenId });
        repository.GetInvitation(tokenId, Arg.Any<CancellationToken>())
            .Returns((new GameInvitationToken(), new GameInvitation()));

        await service.InvitePlayer(gameId, username);

        await producer.Received(1).SendAsync(EventType.PlayerInvitationCreated, tokenId);
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
        gameRepository.GetGame(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        userLookupService.GetAsync(username).Returns(new GeneralUser { UserId = userId });
        repository.InvalidateAndCreateInvitation(Arg.Any<CreateGameInvitationEntity>(), Arg.Any<CancellationToken>())
            .Returns(new GameInvitationToken { TokenId = tokenId });
        repository.GetInvitation(tokenId, Arg.Any<CancellationToken>())
            .Returns((new GameInvitationToken(), new GameInvitation()));

        await service.InviteReader(gameId, username);

        await repository.Received(1).InvalidateAndCreateInvitation(
            Arg.Is<CreateGameInvitationEntity>(e => e.UserId == userId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcceptPlayerInvitationAndRemoveToken()
    {
        var tokenId = Invitation(TokenType.GamePlayerInvitation);
        repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(true);

        await service.AcceptInvitation(tokenId);

        await repository.Received(1).RemoveInvitation(tokenId, Arg.Any<CancellationToken>());
        await producer.Received(1).SendAsync(EventType.PlayerInvitationAccepted, tokenId);
    }

    [Fact]
    public async Task AcceptReaderInvitationAndSubscribe()
    {
        var gameId = Guid.NewGuid();
        var tokenId = Invitation(TokenType.GameReaderInvitation, gameId);
        repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(true);
        subscriptionService.SubscribeAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new Subscription());

        await service.AcceptInvitation(tokenId);

        await subscriptionService.Received(1).SubscribeAsync(gameId, Arg.Any<CancellationToken>());
        await producer.Received(1).SendAsync(EventType.ReaderInvitationAccepted, tokenId);
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
        var gameId = Guid.NewGuid();
        var tokenId = Invitation(tokenType, gameId);
        repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(false);
        subscriptionService.SubscribeAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new Subscription());

        await service.AcceptInvitation(tokenId);

        await repository.Received(1).RemoveInvitation(tokenId, Arg.Any<CancellationToken>());
        await producer.DidNotReceive().SendAsync(announcement, Arg.Any<Guid>());
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
        repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(false);

        await service.RejectInvitation(tokenId);

        await repository.Received(1).RemoveInvitation(tokenId, Arg.Any<CancellationToken>());
        await producer.DidNotReceive().SendAsync(announcement, Arg.Any<Guid>());
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
        repository.RemoveInvitation(tokenId, Arg.Any<CancellationToken>()).Returns(true);

        await service.RejectInvitation(tokenId);

        await producer.Received(1).SendAsync(announcement, tokenId);
    }

    /// <summary>A live invitation of any kind, addressed to the current user.</summary>
    private Guid Invitation(TokenType tokenType, Guid? gameId = null)
    {
        var tokenId = Guid.NewGuid();
        repository.GetInvitation(tokenId, Arg.Any<CancellationToken>()).Returns((
            new GameInvitationToken
            {
                TokenId = tokenId,
                UserId = currentUserId,
                EntityId = gameId ?? Guid.NewGuid(),
                TokenType = tokenType,
                CreatedUtc = DateTimeOffset.UtcNow
            },
            new GameInvitation()));
        return tokenId;
    }

    [Fact]
    public async Task AnnounceTheAssistantSeatTheAcceptanceGranted()
    {
        var tokenId = Invitation(TokenType.GameAssistantInvitation);
        repository.AcceptAssistantInvitation(
                Arg.Any<AddAssistantEntity>(), tokenId, Arg.Any<CancellationToken>())
            .Returns(true);

        await service.AcceptInvitation(tokenId);

        await producer.Received(1).SendAsync(EventType.AssignmentRequestAccepted, tokenId);
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
        var tokenId = Invitation(TokenType.GameAssistantInvitation);
        repository.AcceptAssistantInvitation(
                Arg.Any<AddAssistantEntity>(), tokenId, Arg.Any<CancellationToken>())
            .Returns(false);

        await service.AcceptInvitation(tokenId);

        await repository.Received(1).AcceptAssistantInvitation(
            Arg.Any<AddAssistantEntity>(), tokenId, Arg.Any<CancellationToken>());
        await producer.DidNotReceive().SendAsync(EventType.AssignmentRequestAccepted, Arg.Any<Guid>());
    }


    [Fact]
    public async Task ThrowNotFoundWhenInvitationDoesNotExist()
    {
        var tokenId = Guid.NewGuid();
        repository.GetInvitation(tokenId, Arg.Any<CancellationToken>())
            .Returns(((GameInvitationToken?)null, (GameInvitation?)null));

        var act = async () => await service.AcceptInvitation(tokenId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }
}
