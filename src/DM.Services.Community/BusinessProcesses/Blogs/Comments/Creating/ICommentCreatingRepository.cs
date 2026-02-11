using System.Threading.Tasks;
using PublicationDal = DM.Services.DataAccess.BusinessObjects.Blogs.Publication;
using DM.Services.DataAccess.RelationalStorage;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Creating;

/// <summary>
/// Creating comments storage
/// </summary>
public interface ICommentCreatingRepository
{
    /// <summary>
    /// Create comment from DAL
    /// </summary>
    /// <param name="comment">DAL model for comment</param>
    /// <param name="publicationUpdate">Updating for parent publication (denormalize)</param>
    /// <returns></returns>
    Task<Services.Common.Dto.Comment> Create(Comment comment, IUpdateBuilder<PublicationDal> publicationUpdate);
}
