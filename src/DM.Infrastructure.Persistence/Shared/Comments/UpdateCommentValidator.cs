using DM.Domain.Core.Comments;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// Validator for comment updating DTO model
/// </summary>
internal class UpdateCommentValidator : AbstractValidator<UpdateComment>
{
    /// <inheritdoc />
    public UpdateCommentValidator(IBbCodeNestingLimit nestingLimit)
    {
        RuleFor(c => c.CommentId)
            .NotEmpty();
        RuleFor(c => c.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long);

        // Refused on the edit path as well as on creation: an edit is how most
        // of them would arrive. See IBbCodeNestingLimit.
        RuleFor(c => c.Text)
            .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid);

        // The Comment surface does not declare [private], and an edit is the
        // other way the tag gets into a stored comment.
        RuleFor(c => c.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(c => PrivateBlockMarkup.DescribeSurfaceRefusal(c.Text));
    }
}
