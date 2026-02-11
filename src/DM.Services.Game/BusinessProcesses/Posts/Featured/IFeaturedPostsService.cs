using System.Threading.Tasks;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Posts.Featured;

/// <summary>
/// Service for fetching featured posts
/// </summary>
public interface IFeaturedPostsService
{
    /// <summary>
    /// Get featured posts (best of week and last with plus)
    /// </summary>
    Task<FeaturedPostsEnvelope> Get();
}
