using DM.Domain.Core.Comments;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// Validator for comment creation DTO model
/// </summary>
internal class CreateCommentValidator : AbstractValidator<CreateComment>
{
    /// <inheritdoc />
    public CreateCommentValidator()
    {
        RuleFor(c => c.Text)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(c => c.EntityId)
            .NotEmpty();
    }
}
