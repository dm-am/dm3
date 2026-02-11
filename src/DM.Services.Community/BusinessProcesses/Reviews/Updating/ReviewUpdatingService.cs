using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Community.BusinessProcesses.Reviews.Validation;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.RelationalStorage;
using FluentValidation;
using DbReview = DM.Services.DataAccess.BusinessObjects.Common.Review;

namespace DM.Services.Community.BusinessProcesses.Reviews.Updating;

/// <inheritdoc />
internal class ReviewUpdatingService : IReviewUpdatingService
{
    private readonly IValidator<UpdateReview> _validator;
    private readonly IReviewReadingService _readingService;
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly IReviewUpdatingRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IReviewEligibilityService _eligibilityService;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public ReviewUpdatingService(
        IValidator<UpdateReview> validator,
        IReviewReadingService readingService,
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        IReviewUpdatingRepository repository,
        IIdentityProvider identityProvider,
        IReviewEligibilityService eligibilityService,
        IDateTimeProvider dateTimeProvider)
    {
        _validator = validator;
        _readingService = readingService;
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _identityProvider = identityProvider;
        _eligibilityService = eligibilityService;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Review> Update(UpdateReview updateReview)
    {
        await _validator.ValidateAndThrowAsync(updateReview);
        var review = await _readingService.Get(updateReview.ReviewId);

        var reviewChanges = _updateBuilderFactory.Create<DbReview>(review.Id);
        if (updateReview.Approved.HasValue && updateReview.Approved.Value != review.Approved)
        {
            _intentionManager.ThrowIfForbidden(ReviewIntention.Approve, review);
            reviewChanges.MaybeField(r => r.IsApproved, updateReview.Approved);
        }

        if (!string.IsNullOrEmpty(updateReview.Text))
        {
            _intentionManager.ThrowIfForbidden(ReviewIntention.Edit, review);

            // Check 24-hour edit window for non-platform reviews (admins can edit anytime)
            var currentUser = _identityProvider.Current.User;
            if (currentUser.Role != UserRole.Admin && !_eligibilityService.CanEditReview(review))
            {
                throw new HttpException(HttpStatusCode.Forbidden,
                    "Reviews can only be edited within 24 hours of creation");
            }

            #pragma warning disable CS8603 // Possible null reference return - false positive with MaybeField fluent chain
            reviewChanges.MaybeField(r => r.Text, updateReview.Text?.Trim());
            #pragma warning restore CS8603
            reviewChanges.Field(r => r.ModifiedUtc, _dateTimeProvider.Now);
            reviewChanges.Field(r => r.ModifiedByUserId, currentUser.UserId);
        }

        var result = await _repository.Update(reviewChanges);
        return result;
    }
}