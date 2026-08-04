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
    /// Count posts in room
    /// </summary>
    Task<int> Count(Guid roomId, Guid userId);

    /// <summary>
    /// Get posts in room with paging
    /// </summary>
    Task<IEnumerable<Post>> Get(Guid roomId, PagingData paging, Guid userId);

    /// <summary>
    /// Get single post
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
    /// <param name="postId">Post identifier</param>
    /// <param name="deletedByUserId">User who removed the post</param>
    Task Delete(Guid postId, Guid deletedByUserId);

    /// <summary>
    /// Decrement author's quantity rating (post count)
    /// </summary>
    Task DecrementAuthorQuantityRating(Guid authorId);

    #endregion
}
