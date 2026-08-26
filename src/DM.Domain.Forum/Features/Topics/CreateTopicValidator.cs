using DM.Domain.Core.Configuration;
using DM.Domain.Core.Content;
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

        // The body of a topic renders on the Comment surface, which does not
        // declare [private]. The tag is not markup there, so it hides nothing
        // and the line is published with the tag still around it.
        RuleFor(t => t.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(t => PrivateBlockMarkup.DescribeSurfaceRefusal(t.Text));
    }
}
