using DM.Domain.Core.Exceptions;
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
    public CreatePostValidator()
    {
        RuleFor(p => p.RoomId)
            .NotEmpty().WithMessage(ValidationError.Empty);
        RuleFor(p => p.GameText)
            .NotEmpty().WithMessage(ValidationError.Empty);

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
