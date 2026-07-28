using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.Achievements;

/// <inheritdoc />
internal class CreateAchievementTypeValidator : AbstractValidator<CreateAchievementType>
{
    /// <inheritdoc />
    public CreateAchievementTypeValidator()
    {
        RuleFor(t => t.Code)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(64).WithMessage(ValidationError.Long);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(128).WithMessage(ValidationError.Long);

        RuleFor(t => t.Threshold)
            .GreaterThan(0).WithMessage(ValidationError.Invalid);

        RuleFor(t => t.Tier)
            .InclusiveBetween(1, 4).WithMessage(ValidationError.Invalid)
            .When(t => t.Tier.HasValue);

        RuleFor(t => t.AchievementCategoryId)
            .NotEmpty().WithMessage(ValidationError.Empty);
    }
}

/// <inheritdoc />
internal class UpdateAchievementTypeValidator : AbstractValidator<UpdateAchievementType>
{
    /// <inheritdoc />
    public UpdateAchievementTypeValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(128).WithMessage(ValidationError.Long)
            .When(t => t.Title != null);

        RuleFor(t => t.Threshold)
            .GreaterThan(0).WithMessage(ValidationError.Invalid)
            .When(t => t.Threshold.HasValue);

        RuleFor(t => t.Tier)
            .InclusiveBetween(1, 4).WithMessage(ValidationError.Invalid)
            .When(t => t.Tier.HasValue);
    }
}

/// <inheritdoc />
internal class UpdateAchievementCategoryValidator : AbstractValidator<UpdateAchievementCategory>
{
    /// <inheritdoc />
    public UpdateAchievementCategoryValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(c => c.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(128).WithMessage(ValidationError.Long)
            .When(c => c.Title != null);
    }
}
