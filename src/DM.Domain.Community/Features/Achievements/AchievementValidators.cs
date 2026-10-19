using DM.Domain.Community.Features.Catalogs;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.Achievements;

/// <inheritdoc />
internal class CreateAchievementTypeValidator : AbstractValidator<CreateAchievementType>
{
    public CreateAchievementTypeValidator()
    {
        RuleFor(t => t.Code)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(CatalogFieldLimits.CodeMaxLength).WithMessage(ValidationError.Long);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(CatalogFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(t => t.Threshold)
            .GreaterThan(0).WithMessage(ValidationError.Invalid);

        RuleFor(t => t.Tier)
            .InclusiveBetween(TierScale.MinTier, TierScale.MaxTier).WithMessage(ValidationError.Invalid)
            .When(t => t.Tier.HasValue);

        RuleFor(t => t.AchievementCategoryId)
            .NotEmpty().WithMessage(ValidationError.Empty);
    }
}

/// <inheritdoc />
internal class UpdateAchievementTypeValidator : AbstractValidator<UpdateAchievementType>
{
    public UpdateAchievementTypeValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(CatalogFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long)
            .When(t => t.Title != null);

        RuleFor(t => t.Threshold)
            .GreaterThan(0).WithMessage(ValidationError.Invalid)
            .When(t => t.Threshold.HasValue);

        RuleFor(t => t.Tier)
            .InclusiveBetween(TierScale.MinTier, TierScale.MaxTier).WithMessage(ValidationError.Invalid)
            .When(t => t.Tier.HasValue);
    }
}

/// <inheritdoc />
internal class UpdateAchievementCategoryValidator : AbstractValidator<UpdateAchievementCategory>
{
    public UpdateAchievementCategoryValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(c => c.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(CatalogFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long)
            .When(c => c.Title != null);

        RuleFor(c => c.Description)
            .MaximumLength(CatalogFieldLimits.DescriptionMaxLength).WithMessage(ValidationError.Long)
            .When(c => c.Description != null);
    }
}
