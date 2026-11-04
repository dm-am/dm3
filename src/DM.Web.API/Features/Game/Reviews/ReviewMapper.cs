using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Game.Features.PostReviews;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// Compile-time mapper for game and post reviews.
/// </summary>
/// <remarks>
/// The review texts name their author, as a comment does: the review editor reads
/// AuthorEdit, the BBCode as saved, which nobody else is given.
/// </remarks>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
public partial class ReviewMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public ReviewMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain post review to its response DTO
    /// </summary>
    public PostReviewDto ToPostReview(PostReview review)
    {
        var result = ToPostReviewCore(review);
        result.Text?.BindAuthor(review.Author?.UserId);
        return result;
    }

    /// <summary>
    /// Domain game review to its response DTO
    /// </summary>
    public GameReviewDto ToGameReview(GameReview review)
    {
        var result = ToGameReviewCore(review);
        result.Text?.BindAuthor(review.Author?.UserId);
        return result;
    }

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial PostReviewDto ToPostReviewCore(PostReview review);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    [MapperIgnoreTarget(nameof(GameReviewDto.Viewer))]
    private partial GameReviewDto ToGameReviewCore(GameReview review);
}
