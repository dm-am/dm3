using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Repository for post operations
/// </summary>
public interface IPostRepository
{
    #region Read

    /// <summary>
    /// Count the posts of the room the query admits
    /// </summary>
    Task<int> Count(Guid roomId, RoomPostsQuery query, Guid userId);

    /// <summary>
    /// Get a page of the posts of the room the query admits, in the order it asks
    /// for, each carrying its <see cref="Post.Number"/> in the room
    /// </summary>
    Task<IEnumerable<Post>> Get(Guid roomId, RoomPostsQuery query, PagingData paging, Guid userId);

    /// <summary>
    /// Get single post, carrying its <see cref="Post.Number"/> in the room
    /// </summary>
    Task<Post?> Get(Guid postId, Guid userId);

    /// <summary>
    /// Get posts with rating info (global search with filters).
    /// To fetch a single user's best post, filter via <see cref="PostsQuery"/>
    /// (author username + rating sort + take=1).
    /// </summary>
    /// <param name="query">Filters, sort and paging</param>
    /// <param name="viewerId">
    /// Who is reading. Does NOT widen or narrow the feed — that is fixed by the
    /// query itself, which admits only open rooms of approved, non-draft games.
    /// It decides one thing: whether each attached game reports the reader as a
    /// subscriber, which is what puts "reader" in its participation list.
    /// <see cref="Guid.Empty" /> for a guest, and then no lookup is made.
    /// </param>
    Task<(IEnumerable<Post> Posts, int TotalCount)> GetRated(PostsQuery query, Guid viewerId);

    #endregion

    #region Write

    /// <summary>
    /// Create new post
    /// </summary>
    Task<Post> Create(CreatePostEntity createPost);

    /// <summary>
    /// Update post
    /// </summary>
    Task<Post?> Update(UpdatePostEntity updatePost);

    /// <summary>
    /// Delete post
    /// </summary>
    /// <remarks>
    /// The author's post count and the post's attachments travel with the mark,
    /// and the mark is conditional on the post still being there: the answer says
    /// whether this call is the removal that happened, so a caller that has
    /// counters or events of its own does not repeat them for a post somebody
    /// else removed first.
    /// </remarks>
    /// <param name="postId">Post identifier</param>
    /// <param name="deletedByUserId">User who removed the post</param>
    /// <returns>Whether this call is the one that removed the post</returns>
    Task<bool> Delete(Guid postId, Guid deletedByUserId);

    #endregion
}
