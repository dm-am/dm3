using DM.Domain.Core.Comments;
using DM.Domain.Core.Content;
using FluentValidation;

namespace DM.Infrastructure.Persistence.Shared.Comments;

/// <summary>
/// Validator for comment updating DTO model
/// </summary>
internal class UpdateCommentValidator : AbstractValidator<UpdateComment>
{
    public UpdateCommentValidator(IBbCodeNestingLimit nestingLimit)
    {
        RuleFor(c => c.CommentId)
            .NotEmpty();
        // Null reaches NotEmpty, the first rule of the chain.
        RuleFor(c => c.Text!).CommentText(nestingLimit);
    }
}
