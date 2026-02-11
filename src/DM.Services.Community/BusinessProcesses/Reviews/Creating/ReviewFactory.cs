using System;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Common;

namespace DM.Services.Community.BusinessProcesses.Reviews.Creating;

/// <inheritdoc />
internal class ReviewFactory : IReviewFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public ReviewFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Review Create(CreateReview createReview, Guid userId, bool isApproved)
    {
        return new Review
        {
            ReviewId = _guidFactory.Create(),
            UserId = userId,
            TargetType = createReview.TargetType,
            TargetId = createReview.TargetId,
            CreatedUtc = _dateTimeProvider.Now,
            Text = createReview.Text?.Trim(),
            IsApproved = isApproved
        };
    }

    /// <inheritdoc />
    public Review CreatePostReview(
        Guid postId,
        Guid authorId,
        Guid postAuthorId,
        Guid gameId,
        ReviewSign sign,
        ReviewReasonType? reasonType = null)
    {
        return new Review
        {
            ReviewId = _guidFactory.Create(),
            UserId = authorId,
            TargetType = ReviewTargetType.Post,
            TargetId = postId,
            CreatedUtc = _dateTimeProvider.Now,
            Text = null, // Post reviews don't have text
            IsApproved = true, // Post reviews don't need approval
            SignValue = (short)sign,
            ReasonType = reasonType,
            PostAuthorId = postAuthorId,
            GameId = gameId
        };
    }
}