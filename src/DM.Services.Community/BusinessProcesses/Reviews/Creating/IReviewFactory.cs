using System;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;

namespace DM.Services.Community.BusinessProcesses.Reviews.Creating;

/// <summary>
/// Factory for review DAL model
/// </summary>
internal interface IReviewFactory
{
    /// <summary>
    /// Create review DAL model
    /// </summary>
    /// <param name="createReview"></param>
    /// <param name="userId"></param>
    /// <param name="isApproved"></param>
    /// <returns></returns>
    Review Create(CreateReview createReview, Guid userId, bool isApproved);

    /// <summary>
    /// Create post review DAL model
    /// </summary>
    /// <param name="postId">Target post ID</param>
    /// <param name="authorId">Review author ID</param>
    /// <param name="postAuthorId">Post author ID (denormalized)</param>
    /// <param name="gameId">Game ID (denormalized)</param>
    /// <param name="sign">Review sign</param>
    /// <param name="reasonType">Optional reason type</param>
    /// <returns>Review DAL model</returns>
    Review CreatePostReview(
        Guid postId,
        Guid authorId,
        Guid postAuthorId,
        Guid gameId,
        ReviewSign sign,
        ReviewReasonType? reasonType = null);
}