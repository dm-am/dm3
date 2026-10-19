using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// The rules a comment text answers on creation and on edit alike.
/// </summary>
internal static class CommentTextRules
{
    /// <remarks>
    /// Text the renderer would refuse is refused here, where the author can still fix it
    /// (see IBbCodeNestingLimit). The Comment surface does not declare [private], so the tag
    /// would be published around the line it was meant to hide.
    /// </remarks>
    public static IRuleBuilderOptions<T, string> CommentText<T>(
        this IRuleBuilder<T, string> rule, IBbCodeNestingLimit nestingLimit) =>
        rule
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long)
            .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage((_, text) => PrivateBlockMarkup.DescribeSurfaceRefusal(text));
}
