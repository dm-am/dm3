using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Game.Features.Games;
using DM.Infrastructure.Persistence.Shared.Comments;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DtoPostEdit = DM.Domain.Game.Features.Posts.PostEdit;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Projection formula for the game post - the hottest read of the site.
///
/// Npgsql has refused this projection twice, so any edit here must be
/// followed by a SQL check on a room with posts:
/// - the lead list: concatenating the assistants onto a one-element array
///   inside the projection made the whole query untranslatable, and every
///   post read answered 500. Master and assistants are projected as two
///   members and joined by the model (GameLeadUserIds).
/// - attachments: a post may carry several files, so an inline projection
///   would be a correlated subquery per row; they are filled by a batched
///   read after the page is materialised, the way character portraits are.
///
/// The rating and the number of reviews are derived, not stored, and they
/// belong to this formula: four reads project through it, and a rule about
/// what a post's rating IS must not live in four copies. Two correlated
/// subqueries per row, the same shape GetRated already runs; staying inside
/// the one query keeps paging untouched. Sum over an empty set is NULL in
/// SQL, hence the nullable cast and the coalesce; Count answers 0 on its own.
/// </summary>
internal static class PostMappers
{
    // The lightweight post-context character: no retirement flags, no roster
    // fields; the picture is filled in batch by
    // PostRepository.EnrichWithCharacterPictures.
    private static readonly Expression<Func<DbCharacter, CharacterShort>> CharacterShortProjection =
        c => new CharacterShort
        {
            Id = c.CharacterId,
            Author = GeneralUserProjections.Projection.Splice(c.Author),
            Status = c.Status,
            Name = c.Name,
            IsNpc = c.IsNpc,
            AccessPolicy = c.AccessPolicy
        };

    /// <summary>
    /// The one formula. Internal rather than private so the rated-posts read can
    /// splice it into its own row instead of writing a second copy: the copy is
    /// what shipped an anonymous reader the [private] blocks of every game, by
    /// filling the text and leaving the fields that decide who may see it at
    /// their defaults.
    /// </summary>
    internal static readonly Expression<Func<DbPost, Post>> PostProjection =
        p => new Post
        {
            Id = p.PostId,
            RoomId = p.RoomId,
            AuthorUserId = p.AuthorId,
            GameId = p.Room.GameId,
            CreatedUtc = p.CreatedUtc,
            GameText = p.GameText,
            MetagameText = p.MetagameText!,
            SharePrivateWithAll = p.SharePrivateWithAll,
            RoomViewPrivateText = p.Room.ViewPrivateText,
            GameMasterUserId = p.Room.Game.MasterId,
            GameAssistantUserIds = p.Room.Game.Assistants.Select(a => a.UserId).ToList(),
            PrivateAddresseeSnapshotJson = p.PrivateAddresseeSnapshotJson,
            Rating = p.Reviews.Where(r => !r.IsRemoved).Sum(r => (int?)r.SignValue) ?? 0,
            ReviewCount = p.Reviews.Count(r => !r.IsRemoved),
            Author = GeneralUserProjections.Projection.Splice(p.Author),
            Character = p.Character == null ? null! : CharacterShortProjection.Splice(p.Character),
            Edits = p.Edits
                .OrderByDescending(e => e.ModifiedUtc)
                .Select(e => new DtoPostEdit
                {
                    Id = e.PostEditId,
                    ModifiedUtc = e.ModifiedUtc,
                    Editor = GeneralUserProjections.Projection.Splice(e.Editor)
                })
                .ToList()
        };

    private static readonly Expression<Func<DbPost, Post>> ExpandedPost =
        ExpressionSplicer.Expand(PostProjection);

    /// <summary>EF projection to the domain post</summary>
    public static IQueryable<Post> ProjectToPost(this IQueryable<DbPost> query) =>
        query.Select(ExpandedPost);

    /// <summary>
    /// EF projection for the game-comment delete path; the counters are set by
    /// the repository afterwards, and GameId reads off the discussion key.
    /// </summary>
    public static IQueryable<GameCommentToDelete> ProjectToGameCommentToDelete(
        this IQueryable<DbComment> query) =>
        query.Select(ExpressionSplicer.Expand(
            CommentProjections.WithCommentFields<GameCommentToDelete>(
                c => new GameCommentToDelete())));
}
