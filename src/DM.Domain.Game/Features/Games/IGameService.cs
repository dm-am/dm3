using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Statuses;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Unified service for game CRUD operations
/// </summary>
public interface IGameService
{
    #region Create

    /// <summary>
    /// Create new game
    /// </summary>
    Task<GameDetails> CreateAsync(CreateGame createGame);

    #endregion

    #region Read

    /// <summary>
    /// Get available game tags
    /// </summary>
    Task<IEnumerable<GameTag>> GetTagsAsync();

    /// <summary>
    /// Get games page with filtering
    /// </summary>
    /// <param name="query">Search query</param>
    /// <returns>List of fetched games and paging data</returns>
    Task<(IEnumerable<Game> games, PagingResult paging)> GetGamesAsync(GamesQuery query);

    /// <summary>
    /// Get game by identifier
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task<Game> GetAsync(Guid gameId);

    /// <summary>
    /// Get game by public ID (5-letter URL identifier)
    /// </summary>
    /// <param name="publicId">Public identifier</param>
    Task<Game> GetByPublicIdAsync(string publicId);

    /// <summary>
    /// Identifier of the game a public id addresses.
    /// </summary>
    /// <remarks>
    /// Resolving an address is not reading the game: the operation that follows
    /// authorizes itself. Deliberately not gated on the Read intention,
    /// which is stricter than the visibility filter — a mentor supervising a
    /// draft game fails Read, so gating here refused them the alias form of
    /// routes they may use by GUID. The two address forms now agree.
    /// </remarks>
    /// <param name="publicId">Public identifier</param>
    Task<Guid> ResolveIdByPublicIdAsync(string publicId);

    /// <summary>
    /// Get game details by identifier
    /// </summary>
    Task<GameDetails> GetDetailsAsync(Guid gameId);

    /// <summary>
    /// Get game details by public ID (5-letter URL identifier)
    /// </summary>
    /// <param name="publicId">Public identifier</param>
    Task<GameDetails> GetDetailsByPublicIdAsync(string publicId);

    #endregion

    #region Update

    /// <summary>
    /// Update existing game
    /// </summary>
    /// <param name="updateGame">Update game model</param>
    Task<GameDetails> UpdateAsync(UpdateGame updateGame);

    /// <summary>
    /// Apply a game status transition (start / freeze / finish / close / reopen).
    /// Validates the transition against the current status and authorizes on the
    /// game lead bucket (master + assistant). Kept separate from the generic
    /// settings update so ClosedReason and status timestamps are only ever
    /// changed through the state machine.
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="transition">Requested transition</param>
    Task<GameDetails> ChangeStatusAsync(Guid gameId, ModuleStatusTransition transition);

    /// <summary>
    /// Apply a premoderation transition (send to / remove from premoderation).
    /// Gated Mentor+. Either id form is resolved by the repository, whose
    /// accessibility scope admits the game's leads and the mentor assigned to
    /// the game, so a mentor who does not curate it is refused here exactly as
    /// on the read path.
    /// </summary>
    /// <param name="id">Game public id (5 letters) or GUID</param>
    /// <param name="transition">Requested transition</param>
    Task<GameDetails> ChangePremoderationAsync(string id, ModulePremoderationTransition transition);

    /// <summary>
    /// Reset the recruitment start date (nulls RecruitmentStartedUtc). Admin only.
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task<GameDetails> ResetRecruitmentDateAsync(Guid gameId);

    #endregion

    #region Delete

    /// <summary>
    /// Remove existing game (soft delete)
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task DeleteAsync(Guid gameId);

    #endregion

    #region Users

    /// <summary>
    /// Get list of game assistants
    /// </summary>
    Task<IEnumerable<GeneralUser>> GetAssistantsAsync(Guid gameId);

    /// <summary>
    /// Remove assistant from game
    /// </summary>
    Task RemoveAssistantAsync(Guid gameId, string username);

    #endregion
}
