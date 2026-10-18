using DM.Domain.Core.Dto;
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
/// Both review texts carry the render-context envelope CommentMapper gives a
/// comment, and for the same reason: the review editor asks for author_edit
/// (getPostReviewForEdit, getGameReviewForEdit), and the converter honors it
/// only when the envelope names the author. Without one the author's fetch
/// degrades to Display, and Display is not the source: it turns "--" into
/// &amp;ndash; and reading-only markup into HTML the editor cannot turn back
/// into BBCode, so saving what the editor was handed rewrote the review.
/// Any other viewer's author_edit still degrades to Display.
/// </remarks>
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
    public PostReviewDto ToPostReview(PostReview review)
    {
        var result = ToPostReviewCore(review);
        AttachAuthor(result.Text, review.Author);
        return result;
    }

    /// <summary>
    /// Domain game review to its response DTO
    /// </summary>
    public GameReviewDto ToGameReview(GameReview review)
    {
        var result = ToGameReviewCore(review);
        AttachAuthor(result.Text, review.Author);
        return result;
    }

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial PostReviewDto ToPostReviewCore(PostReview review);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial GameReviewDto ToGameReviewCore(GameReview review);

    private static void AttachAuthor(BbText? text, GeneralUser? author)
    {
        if (text is null)
        {
            return;
        }

        text.Context = new RenderContextEnvelope
        {
            Surface = text.Surface,
            PostAuthorUserId = author?.UserId
        };
    }
}
