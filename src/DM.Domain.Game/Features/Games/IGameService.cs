using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Statuses;

namespace DM.Domain.Game.Features.Games;

public interface IGameService
{
    Task<GameDetails> CreateAsync(CreateGame createGame);

    Task<IEnumerable<GameTag>> GetTagsAsync();

    Task<(IEnumerable<Game> games, PagingResult paging)> GetGamesAsync(GamesQuery query);

    Task<Game> GetAsync(Guid gameId, CancellationToken ct = default);

    Task<Game> GetByPublicIdAsync(string publicId);

    /// <summary>
    /// Identifier of the game a public id addresses.
    /// </summary>
    /// <remarks>
    /// Not gated on Read, stricter than visibility: the operation that follows authorizes itself,
    /// so both address forms agree.
    /// </remarks>
    Task<Guid> ResolveIdByPublicIdAsync(string publicId);

    Task<GameDetails> GetDetailsAsync(Guid gameId);

    Task<GameDetails> GetDetailsByPublicIdAsync(string publicId);

    Task<GameDetails> UpdateAsync(UpdateGame updateGame);

    /// <summary>
    /// Apart from the settings update, so ClosedReason and the status dates move only through the machine.
    /// </summary>
    Task<GameDetails> ChangeStatusAsync(Guid gameId, ModuleStatusTransition transition);

    /// <summary>
    /// <paramref name="id"/> is a public id or a GUID
    /// </summary>
    Task<GameDetails> ChangePremoderationAsync(string id, ModulePremoderationTransition transition);

    /// <summary>
    /// Admin only
    /// </summary>
    Task<GameDetails> ResetRecruitmentDateAsync(Guid gameId);

    /// <summary>
    /// Soft delete
    /// </summary>
    Task DeleteAsync(Guid gameId);

    Task<IEnumerable<GeneralUser>> GetAssistantsAsync(Guid gameId);

    Task RemoveAssistantAsync(Guid gameId, string username);
}
