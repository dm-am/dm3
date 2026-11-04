using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Posts;

/// <inheritdoc />
internal class UpdatePostValidator : AbstractValidator<UpdatePost>
{
    public UpdatePostValidator(IBbCodeNestingLimit nestingLimit)
    {
        RuleFor(p => p.PostId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        When(p => p.GameText != default, () =>
        {
            RuleFor(p => p.GameText)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long);

            // An unclosed or nested [private] renders as private and indexes as public.
            RuleFor(p => p.GameText)
                .Must(PrivateBlockMarkup.IsBalanced)
                .WithMessage(p => PrivateBlockMarkup.DescribeBalanceRefusal(p.GameText));

            // Stored, text the renderer refuses renders as nothing for every reader but the author.
            RuleFor(p => p.GameText)
                .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid);
        });

        // The metagame half renders on the Comment surface, which does not declare [private].
        When(p => p.MetagameText != default, () =>
            RuleFor(p => p.MetagameText)
                .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
                .WithMessage(p => PrivateBlockMarkup.DescribeSurfaceRefusal(p.MetagameText)));
    }
}
