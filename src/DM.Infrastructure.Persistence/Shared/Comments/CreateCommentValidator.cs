using DM.Domain.Core.Comments;
using DM.Domain.Core.Content;
using FluentValidation;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// Validator for comment creation DTO model
/// </summary>
internal class CreateCommentValidator : AbstractValidator<CreateComment>
{
    public CreateCommentValidator(IBbCodeNestingLimit nestingLimit)
    {
        RuleFor(c => c.Text).CommentText(nestingLimit);
        RuleFor(c => c.EntityId)
            .NotEmpty();
    }
}
