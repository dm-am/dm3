using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.GameReviews;

public interface IGameReviewRepository
{
    Task<int> CountAsync(Guid gameId);

    Task<IEnumerable<GameReview>> GetAsync(Guid gameId, PagingData paging);

    Task<GameReview?> GetAsync(Guid id);

    /// <summary>
    /// Under the scope of games of <paramref name="viewerId"/>
    /// </summary>
    Task<int> CountAllAsync(Guid viewerId, GameReviewFilter? filter = null);

    /// <inheritdoc cref="CountAllAsync" />
    Task<IEnumerable<GameReview>> GetAllAsync(Guid viewerId, PagingData paging, GameReviewFilter? filter = null);

    Task<bool> ExistsAsync(Guid authorId, Guid gameId);

    Task<GameReview> CreateAsync(CreateGameReviewEntity entity);

    Task<GameReview> UpdateAsync(UpdateGameReviewEntity entity);

    /// <summary>
    /// The user has a post in the game
    /// </summary>
    Task<bool> CanReviewGameAsync(Guid userId, Guid gameId);

    /// <summary>
    /// The user's post count in all games
    /// </summary>
    Task<int> GetUserPostCountAsync(Guid userId);
}
