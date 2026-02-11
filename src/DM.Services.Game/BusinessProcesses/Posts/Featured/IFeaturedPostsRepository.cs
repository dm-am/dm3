using System.Threading.Tasks;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Posts.Featured;

/// <summary>
/// Repository for fetching featured posts
/// </summary>
public interface IFeaturedPostsRepository
{
    /// <summary>
    /// Get the best post of the week (highest rating sum in last 7 days from open rooms)
    /// </summary>
    Task<FeaturedPost?> GetBestOfWeek();

    /// <summary>
    /// Get the last post that received a positive review from open rooms
    /// </summary>
    Task<FeaturedPost?> GetLastWithPlus();
}
