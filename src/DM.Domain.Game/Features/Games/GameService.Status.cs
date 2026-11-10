using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Statuses;
using DM.Domain.Game.Authorization;

namespace DM.Domain.Game.Features.Games;

internal partial class GameService
{
    public async Task<GameDetails> ChangeStatusAsync(Guid gameId, ModuleStatusTransition transition)
    {
        var game = await GetDetailsAsync(gameId);
        var now = _dateTimeProvider.Now;

        // The machine itself, shared with blogs, is ModuleStatusPolicy.
        var (intention, statusEvent) = transition switch
        {
            ModuleStatusTransition.Start or ModuleStatusTransition.Reopen =>
                (GameIntention.SetStatusActive, EventType.StatusGameActive),
            ModuleStatusTransition.Freeze =>
                (GameIntention.SetStatusClosed, EventType.StatusGameFrozen),
            ModuleStatusTransition.Finish =>
                (GameIntention.SetStatusClosed, EventType.StatusGameFinished),
            ModuleStatusTransition.Close =>
                (GameIntention.SetStatusClosed, EventType.StatusGameClosed),
            _ => throw new HttpException(
                HttpStatusCode.BadRequest, RefusalMessage.UnknownStatusTransition)
        };

        // Legality first: an illegal move is 400 whether or not the caller could make a legal one.
        var change = ModuleStatusPolicy.Resolve(
            transition,
            new ModuleLifecycle(game.Status, game.ClosedReason, game.ActivatedUtc, game.ClosedUtc),
            now);
        _intentionManager.ThrowIfForbidden(intention, game);

        var update = new UpdateGameEntity
        {
            GameId = gameId,
            Status = change.Status,
            ClosedReason = change.ClosedReason,
            ClosedUtc = change.ClosedUtc,
            ClearClosedUtc = change.ClearClosedUtc,
            ActivatedUtc = change.ActivatedUtc,
            // A game that closes stops looking for players.
            IsRecruitmentOpen = change.Status == ModuleStatus.Closed ? false : (bool?)null
        };

        var result = await Answered(await _repository.Update(update));
        await _producer.SendAsync(new List<EventType> { EventType.ChangedGame, statusEvent }, gameId);
        return result;
    }

    public async Task<GameDetails> ChangePremoderationAsync(string id, ModulePremoderationTransition transition)
    {
        // The moderation moves are a rank, asked before the read, so a stranger learns nothing of the alias.
        var isAuthorMove = transition == ModulePremoderationTransition.SubmitForApproval;
        if (!isAuthorMove)
        {
            _intentionManager.ThrowIfForbidden(GameIntention.SetStatusModeration);
        }

        var currentUserId = _identityProvider.Current.User.UserId;

        // A mentor reads through the moderation scope: a game in AwaitingEdits has no curator yet.
        var parsed = Guid.TryParse(id, out var guid);
        var game = isAuthorMove
            ? parsed
                // The author reads as the author, even when he holds the rank.
                ? await _repository.GetGameDetails(guid, currentUserId, mayJudgePremoderation: false)
                : await _repository.GetGameDetailsByPublicId(id, currentUserId, mayJudgePremoderation: false)
            : parsed
                ? await _repository.GetGameDetailsForModeration(guid, currentUserId)
                : await _repository.GetGameDetailsByPublicIdForModeration(id, currentUserId);
        if (game == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.GameNotFound);
        }

        var gameId = game.Id;

        // Legality first, as in ChangeStatusAsync.
        var change = ModulePremoderationPolicy.Resolve(
            transition, game.PremoderationStatus, currentUserId);
        if (isAuthorMove)
        {
            _intentionManager.ThrowIfForbidden(GameIntention.SubmitForApproval, game);
        }

        var update = new UpdateGameEntity
        {
            GameId = gameId,
            MentorId = change.MentorId,
            SetMentorId = change.SetMentorId,
            PremoderationStatus = change.Status
        };

        var result = await Answered(await _repository.Update(update));
        await _producer.SendAsync(new List<EventType> { EventType.ChangedGame, EventType.StatusGameModeration }, gameId);
        return result;
    }
}
