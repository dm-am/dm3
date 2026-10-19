using DM.Domain.Core.Configuration;
using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Forum.Features.Topics;

/// <inheritdoc />
internal class CreateTopicValidator : AbstractValidator<CreateTopic>
{
    public CreateTopicValidator()
    {
        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(TopicPolicy.TitleMaxLength).WithMessage(ValidationError.Long);

        // The Comment surface does not declare [private]: the tag would hide nothing there.
        RuleFor(t => t.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(t => PrivateBlockMarkup.DescribeSurfaceRefusal(t.Text));
    }
}
