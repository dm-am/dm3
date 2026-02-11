using System.Threading.Tasks;
using DM.Services.Common.Dto;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Creating;

/// <summary>
/// Service to create new comments on publication
/// </summary>
public interface ICommentCreatingService
{
    /// <summary>
    /// Create new comment
    /// </summary>
    /// <param name="createComment">Create comment DTO model</param>
    /// <returns></returns>
    Task<Comment> Create(CreateComment createComment);
}
