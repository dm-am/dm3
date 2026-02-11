using System.Threading.Tasks;
using DM.Services.Game.Dto.Output;

namespace DM.Services.Game.BusinessProcesses.Posts.Featured;

/// <inheritdoc />
internal class FeaturedPostsService : IFeaturedPostsService
{
    private readonly IFeaturedPostsRepository _repository;

    /// <summary>
    /// Constructor
    /// </summary>
    public FeaturedPostsService(IFeaturedPostsRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<FeaturedPostsEnvelope> Get()
    {
        var bestOfWeek = await _repository.GetBestOfWeek();
        var lastWithPlus = await _repository.GetLastWithPlus();

        return new FeaturedPostsEnvelope
        {
            BestOfWeek = bestOfWeek,
            LastWithPlus = lastWithPlus
        };
    }
}
