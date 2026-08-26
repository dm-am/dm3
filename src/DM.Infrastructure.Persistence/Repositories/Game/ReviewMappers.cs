using System;
using System.Linq;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Game.Features.PostReviews;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Projection formulas for game and post reviews.
/// </summary>
internal static class ReviewMappers
{
    /// <summary>
    /// EF projection to the domain game review; the game title travels via
    /// the navigation join
    /// </summary>
    public static IQueryable<GameReview> ProjectToGameReview(this IQueryable<DbGameReview> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbGameReview, GameReview>>(r => new GameReview
        {
            Id = r.GameReviewId,
            GameId = r.GameId,
            GameTitle = r.Game.Title,
            CreatedUtc = r.CreatedUtc,
            ModifiedUtc = r.ModifiedUtc,
            Text = r.Text,
            Author = GeneralUserProjections.Projection.Splice(r.Author)
        }));

    /// <summary>
    /// EF projection to the domain post review. The sign is stored as its
    /// numeric value; the enum member on the entity is not mapped.
    /// </summary>
    public static IQueryable<PostReview> ProjectToPostReview(this IQueryable<DbPostReview> query) =>
        query.Select(ExpressionSplicer.Expand<Func<DbPostReview, PostReview>>(r => new PostReview
        {
            Id = r.PostReviewId,
            PostId = r.PostId,
            GameId = r.GameId,
            CreatedUtc = r.CreatedUtc,
            ModifiedUtc = r.ModifiedUtc,
            Text = r.Text,
            Sign = (ReviewSign)r.SignValue,
            Author = GeneralUserProjections.Projection.Splice(r.Author),
            PostAuthor = GeneralUserProjections.Projection.Splice(r.PostAuthor)
        }));
}
