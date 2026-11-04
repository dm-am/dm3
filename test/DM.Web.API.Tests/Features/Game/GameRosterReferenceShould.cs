using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.Invitations;
using DM.Domain.Game.Features.Subscriptions;
using DM.Testing;
using DM.Web.API.Features.Game.Invitations;
using DM.Web.API.Features.Game.Users;
using AwesomeAssertions;
using NSubstitute;
using Xunit;
using DomainGameInvitation = DM.Domain.Game.Features.Invitations.GameInvitation;

namespace DM.Web.API.Tests.Features.Game;

/// <summary>
/// The roster and the invitations of a game name a person by the same reference
/// as every other list: with the role and the newbie mark of the account.
/// </summary>
/// <remarks>Built by hand, the reference carried neither, and every line read as a guest.</remarks>
public class GameRosterReferenceShould : UnitTestBase
{
    private readonly IGameInvitationService _invitationService = Mock<IGameInvitationService>();

    [Fact]
    public async Task NameAPlayerOfTheRosterWithTheRoleAndTheNewbieMark()
    {
        var gameId = Guid.NewGuid();
        _invitationService.GetUsers(gameId, Arg.Any<CancellationToken>()).Returns(
        [
            new DM.Domain.Game.Features.Games.GameUser
            {
                User = Moderator(), Role = GameRole.Player, JoinedUtc = DateTimeOffset.UnixEpoch
            }
        ]);
        var service = new GameUserApiService(_invitationService,
            Mock<IGameSubscriptionService>(), Mock<IGameService>(), Mock<IIdentityProvider>());

        var player = (await service.GetUsers(gameId)).Single().User;

        player.Role.Should().Be(UserRole.Moderator);
        player.IsNewbie.Should().BeFalse();
    }

    [Fact]
    public async Task NameAnInvitedModeratorAsAModerator()
    {
        var gameId = Guid.NewGuid();
        _invitationService.GetPendingInvitations(gameId, Arg.Any<CancellationToken>()).Returns(
        [
            new DomainGameInvitation
            {
                TokenId = Guid.NewGuid(),
                GameId = gameId,
                GameTitle = "Игра",
                InvitedUser = Moderator(),
                InvitedBy = new GeneralUser { UserId = Guid.NewGuid(), Username = "Master" },
                TargetRole = GameRole.Player
            }
        ]);

        var invitee = (await new GameInvitationApiService(_invitationService).GetGameInvitations(gameId))
            .Single().InvitedUser;

        invitee.Role.Should().Be(UserRole.Moderator);
        invitee.IsNewbie.Should().BeFalse();
    }

    private static GeneralUser Moderator() => new()
    {
        UserId = Guid.NewGuid(),
        Username = "Moderator",
        Role = UserRole.Moderator,
        QuantityRating = 500
    };
}
