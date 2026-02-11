using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Reviews.Updating;

/// <inheritdoc />
internal class ReviewUpdatingRepository : IReviewUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ReviewUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }
        
    /// <inheritdoc />
    public async Task<Review> Update(IUpdateBuilder<DataAccess.BusinessObjects.Common.Review> updateReview)
    {
        var reviewId = updateReview.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync();
        return await _dbContext.Reviews
            .Where(r => r.ReviewId == reviewId)
            .ProjectTo<Review>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}