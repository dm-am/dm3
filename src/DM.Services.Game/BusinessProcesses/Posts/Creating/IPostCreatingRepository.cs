using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Dto.Output;
using DbPost = DM.Services.DataAccess.BusinessObjects.Games.Posts.Post;
using PostPendency = DM.Services.DataAccess.BusinessObjects.Games.Links.PostPendency;

namespace DM.Services.Game.BusinessProcesses.Posts.Creating;

/// <summary>
/// Storage for post creating
/// </summary>
internal interface IPostCreatingRepository
{
    /// <summary>
    /// Create new game post
    /// </summary>
    /// <param name="post">Post DAL model</param>
    /// <param name="postPendencyUpdates">Post pendency changes</param>
    /// <returns></returns>
    Task<Post> Create(DbPost post, IEnumerable<IUpdateBuilder<PostPendency>> postPendencyUpdates);
}