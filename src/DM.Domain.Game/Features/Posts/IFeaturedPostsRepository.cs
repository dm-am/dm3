using System.Threading.Tasks;
using DM.Domain.Game.Features.Games;

namespace DM.Domain.Game.Features.Posts;

/// <summary>
/// Repository for featured posts operations
/// </summary>
public interface IFeaturedPostsRepository
{
    /// <summary>
    /// Get the best post of the week
    /// </summary>
    Task<FeaturedPost?> GetBestOfWeek();

    /// <summary>
    /// Get the last post that received a positive review
    /// </summary>
    Task<FeaturedPost?> GetLastWithPlus();
}
