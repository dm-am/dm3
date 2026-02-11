using System;
using System.Threading.Tasks;
using DM.Services.DataAccess.BusinessObjects.Common;
using PublicationDal = DM.Services.DataAccess.BusinessObjects.Blogs.Publication;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Community.BusinessProcesses.Blogs.Comments.Dto;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Deleting;

/// <summary>
/// Deleting comment storage
/// </summary>
internal interface ICommentDeletingRepository
{
    /// <summary>
    /// Get single comment to delete by its identifier
    /// </summary>
    /// <param name="commentId"></param>
    /// <returns></returns>
    Task<CommentToDelete?> GetForDelete(Guid commentId);

    /// <summary>
    /// Update existing comment
    /// </summary>
    /// <param name="update">Updated fields</param>
    /// <param name="publicationUpdate">Updating for parent publication (denormalize)</param>
    /// <returns></returns>
    Task Delete(IUpdateBuilder<Comment> update, IUpdateBuilder<PublicationDal> publicationUpdate);
}
