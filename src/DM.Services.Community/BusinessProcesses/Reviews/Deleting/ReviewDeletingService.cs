using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Community.BusinessProcesses.Reviews.Updating;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using Review = DM.Services.DataAccess.BusinessObjects.Common.Review;

namespace DM.Services.Community.BusinessProcesses.Reviews.Deleting;

/// <inheritdoc />
internal class ReviewDeletingService : IReviewDeletingService
{
    private readonly IReviewReadingService _reviewReadingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IReviewUpdatingRepository _repository;
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public ReviewDeletingService(
        IReviewReadingService reviewReadingService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IReviewUpdatingRepository repository,
        DmDbContext dbContext)
    {
        _reviewReadingService = reviewReadingService;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task Delete(Guid id)
    {
        var review = await _reviewReadingService.Get(id);
        _intentionManager.ThrowIfForbidden(ReviewIntention.Delete, review);
        var deleteReview = _updateBuilderFactory.Create<Review>(id).Field(r => r.IsRemoved, true);
        await _repository.Update(deleteReview);

        // Revert post author's QualityRating when post review is deleted
        if (review.TargetType == ReviewTargetType.Post &&
            review.Sign.HasValue &&
            review.Sign.Value != ReviewSign.Neutral &&
            review.PostAuthorId.HasValue)
        {
            var signValue = (int)review.Sign.Value;
            await _dbContext.Users
                .Where(u => u.UserId == review.PostAuthorId.Value)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.QualityRating, x => x.QualityRating - signValue));
        }
    }
}