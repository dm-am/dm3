using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.Awards;

/// <inheritdoc />
internal class CreateAwardTypeValidator : AbstractValidator<CreateAwardType>
{
    /// <inheritdoc />
    public CreateAwardTypeValidator()
    {
        RuleFor(t => t.Code)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(64).WithMessage(ValidationError.Long);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(128).WithMessage(ValidationError.Long);

        RuleFor(t => t.Description)
            .MaximumLength(1000).WithMessage(ValidationError.Long);

        RuleFor(t => t.Tier)
            .InclusiveBetween(1, 4).WithMessage(ValidationError.Invalid)
            .When(t => t.Tier.HasValue);

        RuleFor(t => t.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
    }
}

/// <inheritdoc />
internal class UpdateAwardTypeValidator : AbstractValidator<UpdateAwardType>
{
    /// <inheritdoc />
    public UpdateAwardTypeValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(128).WithMessage(ValidationError.Long)
            .When(t => t.Title != null);

        RuleFor(t => t.Description)
            .MaximumLength(1000).WithMessage(ValidationError.Long)
            .When(t => t.Description != null);

        RuleFor(t => t.Tier)
            .InclusiveBetween(1, 4).WithMessage(ValidationError.Invalid)
            .When(t => t.Tier.HasValue);

        RuleFor(t => t.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid)
            .When(t => t.SortOrder.HasValue);
    }
}

/// <inheritdoc />
internal class CreateContestSeriesValidator : AbstractValidator<CreateContestSeries>
{
    /// <inheritdoc />
    public CreateContestSeriesValidator()
    {
        RuleFor(s => s.ContestType)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        RuleFor(s => s.Number)
            .GreaterThan(0).WithMessage(ValidationError.Invalid);

        // The site started in 2004; an upper bound keeps a mistyped year from
        // creating a series nobody can find.
        RuleFor(s => s.Year)
            .InclusiveBetween(2004, 2100).WithMessage(ValidationError.Invalid);

        RuleFor(s => s.TopicUrl)
            .MaximumLength(500).WithMessage(ValidationError.Long)
            .When(s => s.TopicUrl != null);
    }
}

/// <inheritdoc />
internal class UpdateContestSeriesValidator : AbstractValidator<UpdateContestSeries>
{
    /// <inheritdoc />
    public UpdateContestSeriesValidator()
    {
        RuleFor(s => s.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(s => s.Number)
            .GreaterThan(0).WithMessage(ValidationError.Invalid)
            .When(s => s.Number.HasValue);

        RuleFor(s => s.Year)
            .InclusiveBetween(2004, 2100).WithMessage(ValidationError.Invalid)
            .When(s => s.Year.HasValue);

        RuleFor(s => s.TopicUrl)
            .MaximumLength(500).WithMessage(ValidationError.Long)
            .When(s => s.TopicUrl != null);
    }
}
