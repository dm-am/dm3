using DM.Domain.Core.Comments;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// Validator for comment updating DTO model
/// </summary>
internal class UpdateCommentValidator : AbstractValidator<UpdateComment>
{
    /// <inheritdoc />
    public UpdateCommentValidator()
    {
        RuleFor(c => c.CommentId)
            .NotEmpty();
        RuleFor(c => c.Text)
            .NotEmpty().WithMessage(ValidationError.Empty);
    }
}
