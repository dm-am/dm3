using System;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// API service for game resources
/// </summary>
public interface IGameApiService
{
    /// <summary>
    /// Get list of searched games (full, with players/readers arrays for tooltips)
    /// </summary>
    /// <param name="gamesQuery">Search query</param>
    /// <returns>Envelope for games list</returns>
    Task<ListEnvelope<Game>> Get(GamesQuery gamesQuery);

    /// <summary>
    /// Get list of games as lightweight refs (for sidebars/menus)
    /// </summary>
    /// <param name="gamesQuery">Search query</param>
    /// <returns>Envelope for game refs list</returns>
    Task<ListEnvelope<GameRef>> GetRefs(GamesQuery gamesQuery);

    /// <summary>
    /// Get certain game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>Envelope containing the game</returns>
    Task<Envelope<Game>> Get(Guid gameId);

    /// <summary>
    /// Get certain game by public ID
    /// </summary>
    /// <param name="publicId">Public identifier (5 letters)</param>
    /// <returns>Envelope containing the game</returns>
    Task<Envelope<Game>> GetByPublicId(string publicId);

    /// <summary>
    /// Get certain game details
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>Envelope containing the game details</returns>
    Task<Envelope<GameDetails>> GetDetails(Guid gameId);

    /// <summary>
    /// Get certain game details by public ID
    /// </summary>
    /// <param name="publicId">Public identifier (5 letters)</param>
    /// <returns>Envelope containing the game details</returns>
    Task<Envelope<GameDetails>> GetDetailsByPublicId(string publicId);

    /// <summary>
    /// Resolve a route identifier of a game, given either form
    /// </summary>
    /// <remarks>
    /// Every game-scoped route accepts both forms, so without a shared
    /// resolver each controller carries its own copy of the branch.
    /// </remarks>
    /// <param name="idOrPublicId">Game public id (5 letters) or GUID</param>
    /// <returns>Game identifier</returns>
    Task<Guid> ResolveId(string idOrPublicId);

    /// <summary>
    /// Create new game
    /// </summary>
    /// <param name="request">Game creation request</param>
    /// <returns>Envelope for created game</returns>
    Task<Envelope<GameDetails>> Create(CreateGameRequest request);

    /// <summary>
    /// Update existing game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="game">Game API model</param>
    /// <returns>Envelope for updated game</returns>
    Task<Envelope<GameDetails>> Update(Guid gameId, GameDetails game);

    /// <summary>
    /// Apply a game status transition (start / freeze / finish / close / reopen)
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="request">Requested transition</param>
    /// <returns>Envelope for updated game details</returns>
    Task<Envelope<GameDetails>> ChangeStatus(Guid gameId, GameStatusChangeRequest request);

    /// <summary>
    /// Apply a premoderation transition (send to / remove from premoderation)
    /// </summary>
    /// <param name="id">Game public id (5 letters) or GUID</param>
    /// <param name="request">Requested transition</param>
    /// <returns>Envelope for updated game details</returns>
    Task<Envelope<GameDetails>> ChangePremoderation(string id, GamePremoderationChangeRequest request);

    /// <summary>
    /// Reset the recruitment start date (admin only)
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>Envelope for updated game details</returns>
    Task<Envelope<GameDetails>> ResetRecruitmentDate(Guid gameId);

    /// <summary>
    /// Delete existing game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    Task Delete(Guid gameId);

    /// <summary>
    /// Get all available game tags
    /// </summary>
    /// <returns>List envelope containing all game tags</returns>
    Task<ListEnvelope<Tag>> GetTags();


}
