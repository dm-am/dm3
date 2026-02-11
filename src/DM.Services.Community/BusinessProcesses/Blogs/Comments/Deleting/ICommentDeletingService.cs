using System;
using System.Threading.Tasks;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Deleting;

/// <summary>
/// Service for deleting publication comments
/// </summary>
public interface ICommentDeletingService
{
    /// <summary>
    /// Delete comment by identifier
    /// </summary>
    /// <param name="commentId">Comment identifier</param>
    /// <returns></returns>
    Task Delete(Guid commentId);
}
