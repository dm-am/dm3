using DM.Domain.Core.Comments;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// Validator for comment creation DTO model
/// </summary>
internal class CreateCommentValidator : AbstractValidator<CreateComment>
{
    /// <inheritdoc />
    public CreateCommentValidator(IBbCodeNestingLimit nestingLimit)
    {
        RuleFor(c => c.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long);
        RuleFor(c => c.EntityId)
            .NotEmpty();

        // Text the renderer will refuse is refused here, where the author can
        // still fix it: stored, it renders as nothing for every reader but its
        // author. See IBbCodeNestingLimit.
        RuleFor(c => c.Text)
            .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid);

        // A comment renders on the Comment surface, which does not declare
        // [private]. The tag is not markup here, so it hides nothing and the
        // line is published with the tag still around it.
        RuleFor(c => c.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(c => PrivateBlockMarkup.DescribeSurfaceRefusal(c.Text));
    }
}
