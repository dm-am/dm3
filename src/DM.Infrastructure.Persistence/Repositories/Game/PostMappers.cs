using System;
using System.Linq;
using System.Linq.Expressions;
using DM.Domain.Game.Features.Characters;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence.Shared.Comments;
using DM.Infrastructure.Persistence.Shared.Queries;
using DM.Infrastructure.Persistence.Shared.Users;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DtoPostEdit = DM.Domain.Game.Features.Posts.PostEdit;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <summary>
/// Projection formula for the game post, the hottest read of the site.
/// Any edit here needs a SQL check on a room with posts: Npgsql refuses some shapes.
/// Master and assistants are two members joined by the model (GameLeadUserIds), and
/// attachments are filled by a batched read after the page.
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
            GameShowPrivateMessages = p.Room.Game.ShowPrivateMessages,
            RoomViewPrivateText = p.Room.ViewPrivateText,
            GameMasterUserId = p.Room.Game.MasterId,
            GameAssistantUserIds = p.Room.Game.Assistants.Select(a => a.UserId).ToList(),
            PrivateAddresseeSnapshotJson = p.PrivateAddresseeSnapshotJson,
            Rating = PostFilters.RatingOf.Splice(p),
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

    /// <summary>
    /// EF projection to the domain post. Keeps the posts of removed accounts,
    /// signed by nobody (<see cref="RemovedAuthors"/>).
    /// </summary>
    public static IQueryable<Post> ProjectToPost(this IQueryable<DbPost> query) =>
        query.KeepingRemovedAuthors().Select(ExpandedPost);

    /// <summary>
    /// EF projection for the game-comment delete path; the counters are set by
    /// the repository afterwards, and GameId reads off the discussion key.
    /// </summary>
    public static IQueryable<GameCommentToDelete> ProjectToGameCommentToDelete(
        this IQueryable<DbComment> query) =>
        query.KeepingRemovedAuthors().Select(ExpressionSplicer.Expand(
            CommentProjections.WithCommentFields<GameCommentToDelete>(
                c => new GameCommentToDelete())));
}
