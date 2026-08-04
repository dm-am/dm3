using DM.Domain.Core.Configuration;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Forum.Features.Topics;

/// <summary>
/// Validator for topic creation DTO model
/// </summary>
internal class CreateTopicValidator : AbstractValidator<CreateTopic>
{
    /// <inheritdoc />
    public CreateTopicValidator()
    {
        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(TopicPolicy.TitleMaxLength).WithMessage(ValidationError.Long);
    }
}
