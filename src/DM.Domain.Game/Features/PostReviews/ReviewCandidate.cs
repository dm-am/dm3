using System;

namespace DM.Domain.Game.Features.PostReviews;

/// <summary>
/// A post somebody might review, with the two facts the rules read off it
/// </summary>
public record ReviewCandidate(Guid PostId, Guid AuthorId, Guid GameId);
