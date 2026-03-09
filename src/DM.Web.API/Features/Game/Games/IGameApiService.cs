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
    /// Get list of searched games
    /// </summary>
    /// <param name="gamesQuery">Search query</param>
    /// <returns>Envelope for games list</returns>
    Task<ListEnvelope<Game>> Get(GamesQuery gamesQuery);

    /// <summary>
    /// Get user owned games
    /// </summary>
    /// <returns></returns>
    Task<ListEnvelope<Game>> GetOwn();

    /// <summary>
    /// Get most popular games
    /// </summary>
    /// <returns></returns>
    Task<ListEnvelope<Game>> GetPopular();

    /// <summary>
    /// Get certain game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns></returns>
    Task<Envelope<Game>> Get(Guid gameId);

    /// <summary>
    /// Get certain game details
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns></returns>
    Task<Envelope<GameDetails>> GetDetails(Guid gameId);

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
    /// Delete existing game
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns></returns>
    Task Delete(Guid gameId);

    /// <summary>
    /// Get all available game tags
    /// </summary>
    /// <returns></returns>
    Task<ListEnvelope<Tag>> GetTags();

    /// <summary>
    /// Get game notes (private notepad for GM)
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <returns>Envelope with game notes</returns>
    Task<Envelope<GameNotes>> GetNotes(Guid gameId);

    /// <summary>
    /// Update game notes
    /// </summary>
    /// <param name="gameId">Game identifier</param>
    /// <param name="notes">Notes to update</param>
    /// <returns>Envelope with updated game notes</returns>
    Task<Envelope<GameNotes>> UpdateNotes(Guid gameId, GameNotes notes);
}
