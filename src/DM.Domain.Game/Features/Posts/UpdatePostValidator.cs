using DM.Domain.Core.Content;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Game.Features.Posts;

/// <inheritdoc />
internal class UpdatePostValidator : AbstractValidator<UpdatePost>
{
    /// <inheritdoc />
    public UpdatePostValidator(IBbCodeNestingLimit nestingLimit)
    {
        RuleFor(p => p.PostId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        When(p => p.GameText != default, () =>
        {
            RuleFor(p => p.GameText)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long);

            // An unclosed or nested [private] renders as private and indexes as
            // public — see PrivateBlockMarkup.IsBalanced. Refused on the edit path
            // as well as on creation: an edit is how most of them would arrive.
            RuleFor(p => p.GameText)
                .Must(PrivateBlockMarkup.IsBalanced)
                .WithMessage(p => PrivateBlockMarkup.DescribeBalanceRefusal(p.GameText));

            // Nested past what the renderer will build, the post is stored and
            // then shows as nothing to everyone but its author. See
            // IBbCodeNestingLimit.
            RuleFor(p => p.GameText)
                .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid);
        });

        // The metagame half renders on the Comment surface, which does not
        // declare [private]: the tag is not markup there and the line goes out
        // with the tag around it. Outside the When above because it is a
        // different field with a different rule.
        When(p => p.MetagameText != default, () =>
            RuleFor(p => p.MetagameText)
                .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
                .WithMessage(p => PrivateBlockMarkup.DescribeSurfaceRefusal(p.MetagameText)));
    }
}
