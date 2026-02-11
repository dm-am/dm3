using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Common.Dto;
using DM.Services.Core.Dto;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Reading;

/// <summary>
/// Publication comments storage
/// </summary>
internal interface ICommentReadingRepository
{
    /// <summary>
    /// Count comments of the publication
    /// </summary>
    /// <param name="publicationId">Publication id</param>
    /// <returns>Number of publication comments</returns>
    Task<int> Count(Guid publicationId);

    /// <summary>
    /// Get comments list of the publication
    /// </summary>
    /// <param name="publicationId">Publication id</param>
    /// <param name="paging">Paging data</param>
    /// <returns></returns>
    Task<IEnumerable<Comment>> Get(Guid publicationId, PagingData paging);

    /// <summary>
    /// Get single comment by its identifier
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns>Found comment</returns>
    Task<Comment?> Get(Guid commentId);

    /// <summary>
    /// Get publication IDs by blog ID
    /// </summary>
    /// <param name="blogId">Blog identifier</param>
    /// <returns>List of publication IDs</returns>
    Task<List<Guid>> GetPublicationIdsByBlogId(Guid blogId);
}
