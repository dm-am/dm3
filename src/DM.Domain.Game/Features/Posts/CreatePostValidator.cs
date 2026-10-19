using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Content;
using FluentValidation;

namespace DM.Domain.Game.Features.Posts;

/// <inheritdoc />
internal class CreatePostValidator : AbstractValidator<CreatePost>
{
    // A submitted spec cannot ask the server to roll a pathological number of dice or edges.
    private const int MinEdges = 2;
    private const int MaxEdges = 1000;
    private const int MaxDiceCount = 100;
    private const int MaxExplosionCount = 100;

    public CreatePostValidator(IBbCodeNestingLimit nestingLimit)
    {
        RuleFor(p => p.RoomId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(p => p.GameText)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long);

        // An unclosed or nested [private] renders as private and indexes as public; the author fixes it here.
        RuleFor(p => p.GameText)
            .Must(PrivateBlockMarkup.IsBalanced)
            .WithMessage(p => PrivateBlockMarkup.DescribeBalanceRefusal(p.GameText));

        // The metagame half renders on the Comment surface, which does not declare [private].
        RuleFor(p => p.MetagameText)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(p => PrivateBlockMarkup.DescribeSurfaceRefusal(p.MetagameText));

        // Stored, text the renderer refuses renders as nothing for every reader but the author.
        RuleFor(p => p.GameText)
            .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid);

        RuleForEach(p => p.DiceRolls).ChildRules(roll =>
        {
            roll.RuleFor(r => r.EdgesCount)
                .InclusiveBetween(MinEdges, MaxEdges).WithMessage(ValidationError.Invalid);
            roll.RuleFor(r => r.DiceCount)
                .InclusiveBetween(1, MaxDiceCount).WithMessage(ValidationError.Invalid);
            roll.RuleFor(r => r.ExplosionCount!.Value)
                .InclusiveBetween(0, MaxExplosionCount).WithMessage(ValidationError.Invalid)
                .When(r => r.ExplosionCount.HasValue);
        });
    }
}
