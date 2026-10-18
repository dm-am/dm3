using System;

namespace DM.Domain.Game.Features.PostReviews;

/// <summary>
/// A post somebody might review, with the two facts the rules of a review read
/// off it: who wrote it, and the game whose three-day window it falls in
/// </summary>
/// <param name="PostId">Post identifier</param>
/// <param name="AuthorId">Identifier of the user who wrote the post</param>
/// <param name="GameId">Identifier of the game the post was written in</param>
public record ReviewCandidate(Guid PostId, Guid AuthorId, Guid GameId);
