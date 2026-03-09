using System.Threading.Tasks;


namespace DM.Domain.Game.Features.Posts;
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
