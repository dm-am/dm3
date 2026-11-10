using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Features.PostReviews;
using DM.Testing.Dsl;
using DM.Testing;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;

namespace DM.Domain.Game.Tests.Dsl;

public abstract class PostReviewServiceTestBase : UnitTestBase
{
    protected readonly IValidator<CreatePostReview> createValidator;
    protected readonly IValidator<UpdatePostReview> updateValidator;
    protected readonly IIntentionManager intentionManager;
    protected readonly IPostReviewRepository repository;
    protected readonly IIdentityProvider identityProvider;
    protected readonly IGuidFactory guidFactory;
    protected readonly IDateTimeProvider dateTimeProvider;
    private protected readonly PostReviewService service;
    protected readonly Guid currentUserId;

    protected PostReviewServiceTestBase()
    {
        createValidator = Mock<IValidator<CreatePostReview>>();
        createValidator.ValidateAsync(Arg.Any<CreatePostReview>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        updateValidator = Mock<IValidator<UpdatePostReview>>();
        updateValidator.ValidateAsync(Arg.Any<UpdatePostReview>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        intentionManager = Mock<IIntentionManager>();

        repository = Mock<IPostReviewRepository>();

        currentUserId = Guid.NewGuid();

        // Past probation by default. Zero is the newbie answer, and every case
        // about a newbie says so for itself; leaving it as the default made the
        // probation rule fire in cases that are not about it at all.
        repository.GetUserPostCountAsync(currentUserId).Returns(ProbationPolicy.NewbiePostThreshold);

        // Nothing reviewed by default, as the posts and games of a fresh case are.
        Reviewed();
        RecentlyReviewedGames();

        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId, UserRole.RegularUser));

        guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new PostReviewService(
            createValidator,
            updateValidator,
            intentionManager,
            repository,
            identityProvider,
            guidFactory,
            dateTimeProvider);
    }

    protected void Reviewed(params Guid[] postIds) =>
        repository.SelectReviewedPostIdsAsync(currentUserId, Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(callInfo => (IReadOnlyCollection<Guid>)callInfo.Arg<IReadOnlyCollection<Guid>>()
                .Where(postIds.Contains).ToArray());

    protected void RecentlyReviewedGames(params Guid[] gameIds) =>
        repository.SelectGamesReviewedSinceAsync(currentUserId, Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<DateTimeOffset>())
            .Returns(callInfo => (IReadOnlyCollection<Guid>)callInfo.Arg<IReadOnlyCollection<Guid>>()
                .Where(gameIds.Contains).ToArray());
}
