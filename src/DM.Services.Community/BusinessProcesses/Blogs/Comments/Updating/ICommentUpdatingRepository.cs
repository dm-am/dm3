using System.Threading.Tasks;
using DM.Services.DataAccess.RelationalStorage;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Updating;

/// <summary>
/// Updating comment storage
/// </summary>
public interface ICommentUpdatingRepository
{
    /// <summary>
    /// Update single comment
    /// </summary>
    /// <param name="update">Update comment</param>
    /// <returns></returns>
    Task<Services.Common.Dto.Comment> Update(IUpdateBuilder<Comment> update);
}
