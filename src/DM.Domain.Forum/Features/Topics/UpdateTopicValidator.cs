using DM.Domain.Core.Configuration;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Forum.Features.Topics;

/// <summary>
/// Validator for topic modification DTO model
/// </summary>
internal class UpdateTopicValidator : AbstractValidator<UpdateTopic>
{
    /// <inheritdoc />
    public UpdateTopicValidator()
    {
        RuleFor(t => t.TopicId)
            .NotEmpty().WithMessage(ValidationError.Empty);

        When(t => t.Title != null, () =>
            RuleFor(t => t.Title)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(TopicPolicy.TitleMaxLength).WithMessage(ValidationError.Long));

        // The Comment surface does not declare [private], and an edit is the
        // other way the tag gets into a stored topic body.
        When(t => t.Text != null, () =>
            RuleFor(t => t.Text)
                .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
                .WithMessage(t => PrivateBlockMarkup.DescribeSurfaceRefusal(t.Text)));
    }
}
