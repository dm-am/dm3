using DM.Domain.Game.Features.GameReviews;
using DM.Domain.Game.Features.PostReviews;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using Riok.Mapperly.Abstractions;

namespace DM.Web.API.Features.Game.Reviews;

/// <summary>
/// Compile-time mapper for game and post reviews.
///
/// No render-context envelope on the review texts - a decision, not an
/// omission. The review editor does ask for author_edit
/// (getPostReviewForEdit), and without an owner id the converter degrades
/// the request to Display, which fails closed: nothing leaks, and plain
/// review markup survives the editor round-trip. The degradation costs
/// fidelity only for privileged blocks, which review texts do not carry -
/// an authorized [mod] use case for reviews is the trigger to add the same
/// envelope step CommentMapper takes.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
internal partial class ReviewMapper
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
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial PostReviewDto ToPostReview(PostReview review);

    /// <summary>
    /// Domain game review to its response DTO
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial GameReviewDto ToGameReview(GameReview review);
}
