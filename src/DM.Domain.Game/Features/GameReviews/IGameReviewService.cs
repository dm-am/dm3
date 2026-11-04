using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Game.Features.GameReviews;

public interface IGameReviewService
{
    Task<GameReview> CreateAsync(CreateGameReview createReview);

    /// <summary>
    /// True exactly when <see cref="CreateAsync" /> takes a review of the game, the text aside.
    /// </summary>
    Task<bool> MayCreateAsync(Games.Game game);

    /// <summary>
    /// A hidden game answers as a missing game, a review of another game as a missing review
    /// </summary>
    Task<GameReview> GetAsync(Guid gameId, Guid reviewId);

    Task<(IEnumerable<GameReview> Reviews, PagingResult Paging)> GetListAsync(Guid gameId, PagingQuery query);

    /// <summary>
    /// The reviews of the games the reader may open
    /// </summary>
    Task<(IEnumerable<GameReview> Reviews, PagingResult Paging)> GetAllAsync(PagingQuery query, GameReviewFilter? filter = null);

    Task<GameReview> UpdateAsync(UpdateGameReview updateReview);

    Task DeleteAsync(Guid id);

    /// <summary>
    /// True exactly when <see cref="UpdateAsync" /> takes an edit of the review, the text aside.
    /// </summary>
    bool MayEdit(GameReview review);

    /// <summary>
    /// True exactly when <see cref="DeleteAsync" /> takes the removal of the review.
    /// </summary>
    bool MayDelete(GameReview review);
}
