using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Content;
using FluentValidation;

namespace DM.Domain.Game.Features.Posts;

/// <inheritdoc />
internal class CreatePostValidator : AbstractValidator<CreatePost>
{
    // Sane upper bounds so a submitted spec cannot ask the server to roll a
    // pathological number of dice or edges (doc 4.2.2.13).
    private const int MaxEdges = 1000;
    private const int MaxDiceCount = 100;
    private const int MaxExplosionCount = 100;

    /// <inheritdoc />
    public CreatePostValidator(IBbCodeNestingLimit nestingLimit)
    {
        RuleFor(p => p.RoomId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(p => p.GameText)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(BodyTextLimits.MaxLength).WithMessage(ValidationError.Long);

        // An unclosed or nested [private] renders as private and indexes as
        // public — see PrivateBlockMarkup.IsBalanced for which half of that is
        // which. Refused here, where the author can still fix it, and named in
        // the refusal, because a post is long and a code cannot be searched for.
        RuleFor(p => p.GameText)
            .Must(PrivateBlockMarkup.IsBalanced)
            .WithMessage(p => PrivateBlockMarkup.DescribeBalanceRefusal(p.GameText));

        // The metagame half of the same post renders on the Comment surface,
        // which does not declare [private] at all: the tag is not markup there,
        // nothing hides anything, and the line goes out with the tag around it.
        RuleFor(p => p.MetagameText)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(p => PrivateBlockMarkup.DescribeSurfaceRefusal(p.MetagameText));

        // Text the renderer will refuse is refused here too, for the same reason
        // and in the same place: stored, it renders as nothing for every reader
        // but the author, who sees the source and has no way to tell what the
        // complaint is about. See IBbCodeNestingLimit.
        RuleFor(p => p.GameText)
            .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid);

        RuleForEach(p => p.DiceRolls).ChildRules(roll =>
        {
            roll.RuleFor(r => r.EdgesCount)
                .InclusiveBetween(2, MaxEdges).WithMessage(ValidationError.Invalid);
            roll.RuleFor(r => r.DiceCount)
                .InclusiveBetween(1, MaxDiceCount).WithMessage(ValidationError.Invalid);
            roll.RuleFor(r => r.ExplosionCount!.Value)
                .InclusiveBetween(0, MaxExplosionCount).WithMessage(ValidationError.Invalid)
                .When(r => r.ExplosionCount.HasValue);
        });
    }
}
