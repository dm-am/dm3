using System.Threading.Tasks;
using DM.Services.Common.Dto;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Updating;

/// <summary>
/// Service for updating publication comments
/// </summary>
public interface ICommentUpdatingService
{
    /// <summary>
    /// Update existing comment
    /// </summary>
    /// <param name="updateComment">Update comment model</param>
    /// <returns></returns>
    Task<Comment> Update(UpdateComment updateComment);
}
