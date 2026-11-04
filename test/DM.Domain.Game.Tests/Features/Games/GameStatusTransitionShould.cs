using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Statuses;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Games;

public class GameStatusTransitionShould : GameServiceTestBase
{
    private readonly DateTimeOffset now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);
    private UpdateGameEntity? capturedUpdate;

    public GameStatusTransitionShould()
    {
        subscriptionService.GetSubscribersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<UserReference>());

        dateTimeProvider.Now.Returns(now);
    }

    private Guid SetupGame(
        ModuleStatus status,
        ClosedReason closedReason = ClosedReason.None,
        DateTimeOffset? activatedUtc = null,
        DateTimeOffset? closedUtc = null,
        PremoderationStatus premoderationStatus = PremoderationStatus.Approved,
        Guid? masterId = null)
    {
        var gameId = Guid.NewGuid();
        var game = new GameDetails
        {
            Id = gameId,
            Status = status,
            ClosedReason = closedReason,
            ActivatedUtc = activatedUtc,
            ClosedUtc = closedUtc,
            PremoderationStatus = premoderationStatus,
            Master = new GeneralUser { UserId = masterId ?? currentUserId },
            Recruitment = new GameRecruitment()
        };
        repository.GetGameDetails(gameId, currentUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(game);
        // The mentor's two moves read past the accessibility scope, because a game
        // awaiting edits has no curator and the scope would hide it from them.
        repository.GetGameDetailsForModeration(
                gameId, currentUserId, Arg.Any<CancellationToken>()).Returns(game);
        repository.Update(Arg.Do<UpdateGameEntity>(u => capturedUpdate = u), Arg.Any<CancellationToken>())
            .Returns(game);
        return gameId;
    }

    [Fact]
    public async Task StartDraftGameAndSetActivatedUtcOnFirstActivation()
    {
        var gameId = SetupGame(ModuleStatus.Draft);

        await service.ChangeStatusAsync(gameId, ModuleStatusTransition.Start);

        capturedUpdate.Should().NotBeNull();
        capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        capturedUpdate.ActivatedUtc.Should().Be(now);
        await producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameActive)), gameId);
    }

    [Fact]
    public async Task RejectStartOfAnActiveGame()
    {
        var gameId = SetupGame(ModuleStatus.Active);

        var act = async () => await service.ChangeStatusAsync(gameId, ModuleStatusTransition.Start);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FreezeActiveGameWithFrozenReason()
    {
        var gameId = SetupGame(ModuleStatus.Active);

        await service.ChangeStatusAsync(gameId, ModuleStatusTransition.Freeze);

        capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        capturedUpdate.ClosedReason.Should().Be(ClosedReason.Frozen);
        capturedUpdate.ClosedUtc.Should().Be(now);
        capturedUpdate.IsRecruitmentOpen.Should().BeFalse();
        await producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameFrozen)), gameId);
    }

    [Fact]
    public async Task FinishActiveGameWithFinishedReason()
    {
        var gameId = SetupGame(ModuleStatus.Active);

        await service.ChangeStatusAsync(gameId, ModuleStatusTransition.Finish);

        capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        capturedUpdate.ClosedReason.Should().Be(ClosedReason.Finished);
        capturedUpdate.ClosedUtc.Should().Be(now);
        capturedUpdate.IsRecruitmentOpen.Should().BeFalse();
        await producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameFinished)), gameId);
    }

    [Fact]
    public async Task CloseActiveGameWithNoneReasonAndSetClosedUtc()
    {
        var gameId = SetupGame(ModuleStatus.Active);

        await service.ChangeStatusAsync(gameId, ModuleStatusTransition.Close);

        capturedUpdate!.Status.Should().Be(ModuleStatus.Closed);
        capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        capturedUpdate.ClosedUtc.Should().Be(now);
        capturedUpdate.IsRecruitmentOpen.Should().BeFalse();
        await producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameClosed)), gameId);
    }

    [Fact]
    public async Task ReopenAFrozenGame()
    {
        var gameId = SetupGame(ModuleStatus.Closed, ClosedReason.Frozen,
            activatedUtc: now.AddMonths(-2), closedUtc: now.AddDays(-7));

        await service.ChangeStatusAsync(gameId, ModuleStatusTransition.Reopen);

        capturedUpdate!.Status.Should().Be(ModuleStatus.Active);
        capturedUpdate.ClosedReason.Should().Be(ClosedReason.None);
        capturedUpdate.ClearClosedUtc.Should().BeTrue();
        capturedUpdate.ActivatedUtc.Should().BeNull(); // Already activated before
        await producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameActive)), gameId);
    }

    [Fact]
    public async Task ApproveAGameAndClearTheCurator()
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.AwaitingApproval);

        await service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SetApproved);

        capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.Approved);
        capturedUpdate.MentorId.Should().BeNull();
        capturedUpdate.SetMentorId.Should().BeTrue();
        await producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameModeration)), gameId);
    }

    [Fact]
    public async Task ReturnAGameForEditsAndRecordTheCurator()
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.AwaitingApproval);

        await service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SetAwaitingEdits);

        capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.AwaitingEdits);
        capturedUpdate.MentorId.Should().Be(currentUserId);
        capturedUpdate.SetMentorId.Should().BeTrue();
    }

    /// <summary>
    /// The two verdicts are a rank and nothing else, so they are asked of the
    /// targetless intention — before the game is read, which is what keeps a
    /// stranger probing aliases from learning whether one resolves.
    /// </summary>
    [Theory]
    [InlineData(ModulePremoderationTransition.SetApproved)]
    [InlineData(ModulePremoderationTransition.SetAwaitingEdits)]
    public async Task AskTheMentorRankForAVerdictAndNothingAboutTheGame(
        ModulePremoderationTransition transition)
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.AwaitingEdits);

        await service.ChangePremoderationAsync(gameId.ToString(), transition);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.SetStatusModeration);
        intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.SubmitForApproval, Arg.Any<GameDetails>());
    }

    [Fact]
    public async Task SubmitAGameAwaitingEditsForApprovalWithoutTouchingTheCurator()
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.AwaitingEdits);

        await service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        capturedUpdate!.PremoderationStatus.Should().Be(PremoderationStatus.AwaitingApproval);
        capturedUpdate.SetMentorId.Should().BeFalse();
        await producer.Received(1).SendAsync(
            Arg.Is<IEnumerable<EventType>>(e => e.Contains(EventType.StatusGameModeration)), gameId);
    }

    /// <summary>
    /// The author's move is a fact about the game, so it is asked of the targeted
    /// intention — and the mentor rank is not asked at all, because the master
    /// making this move normally does not hold it.
    /// </summary>
    [Fact]
    public async Task AskTheGameWhoTheAuthorIsAndNotTheMentorRank()
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.AwaitingEdits);

        await service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.SubmitForApproval, Arg.Any<GameDetails>());
        intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.SetStatusModeration);
    }

    /// <summary>
    /// Illegality is answered before authorization, as on the status machine: the
    /// author gets a 400 naming the move, not a 403 about who they are.
    /// </summary>
    [Fact]
    public async Task RefuseAnIllegalSubmitBeforeAskingWhoTheCallerIs()
    {
        var gameId = SetupGame(ModuleStatus.Draft, premoderationStatus: PremoderationStatus.Approved);

        var act = async () => await service.ChangePremoderationAsync(
            gameId.ToString(), ModulePremoderationTransition.SubmitForApproval);

        await act.Should().ThrowAsync<HttpException>();
        intentionManager.DidNotReceive().ThrowIfForbidden(GameIntention.SubmitForApproval, Arg.Any<GameDetails>());
    }
}
