using System.Threading.Tasks;
using DM.Services.Game.Dto.Output;
using DbPostPendency = DM.Services.DataAccess.BusinessObjects.Games.Links.PostPendency;

namespace DM.Services.Game.BusinessProcesses.PostPendencies.Creating;

/// <summary>
/// Storage for post pendency creating
/// </summary>
internal interface IPostPendencyCreatingRepository
{
    /// <summary>
    /// Save new post pendency
    /// </summary>
    /// <param name="postPendency">DAL model</param>
    /// <returns></returns>
    Task<PostPendency> Create(DbPostPendency postPendency);
}
