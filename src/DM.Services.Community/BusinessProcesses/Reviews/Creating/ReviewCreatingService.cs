using System;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Reviews.Reading;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Core.Exceptions;
using FluentValidation;
using Npgsql;

namespace DM.Services.Community.BusinessProcesses.Reviews.Creating;

/// <inheritdoc />
internal class ReviewCreatingService : IReviewCreatingService
{
    private readonly IValidator<CreateReview> validator;
    private readonly IIntentionManager intentionManager;
    private readonly IReviewFactory factory;
    private readonly IReviewCreatingRepository repository;
    private readonly IIdentityProvider identityProvider;
    private readonly IUserReadingService userReadingService;

    /// <inheritdoc />
    public ReviewCreatingService(
        IValidator<CreateReview> validator,
        IIntentionManager intentionManager,
        IReviewFactory factory,
        IReviewCreatingRepository repository,
        IIdentityProvider identityProvider,
        IUserReadingService userReadingService)
    {
        this.validator = validator;
        this.intentionManager = intentionManager;
        this.factory = factory;
        this.repository = repository;
        this.identityProvider = identityProvider;
        this.userReadingService = userReadingService;
    }

    /// <inheritdoc />
    public async Task<Review> Create(CreateReview createReview)
    {
        await validator.ValidateAndThrowAsync(createReview);
        intentionManager.ThrowIfForbidden(ReviewIntention.Create);

        var authorId = identityProvider.Current.User.UserId;
        if (!string.IsNullOrEmpty(createReview.AuthorLogin))
        {
            var author = await userReadingService.Get(createReview.AuthorLogin);
            authorId = author.UserId;
        }

        if (await repository.UserHasReview(authorId))
        {
            throw new HttpException(HttpStatusCode.Conflict, "User already has a review");
        }

        var review = factory.Create(createReview, authorId, isApproved: true);
        try
        {
            return await repository.Create(review);
        }
        catch (Exception ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new HttpException(HttpStatusCode.Conflict, "User already has a review");
        }
    }
}