using DM.Domain.Community.Features.Catalogs;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Community.Features.Awards;

/// <inheritdoc />
internal class CreateAwardTypeValidator : AbstractValidator<CreateAwardType>
{
    public CreateAwardTypeValidator()
    {
        RuleFor(t => t.Code)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(CatalogFieldLimits.CodeMaxLength).WithMessage(ValidationError.Long);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(CatalogFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(t => t.Description)
            .MaximumLength(CatalogFieldLimits.DescriptionMaxLength).WithMessage(ValidationError.Long);

        RuleFor(t => t.Tier)
            .InclusiveBetween(TierScale.MinTier, TierScale.MaxTier).WithMessage(ValidationError.Invalid)
            .When(t => t.Tier.HasValue);

        RuleFor(t => t.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid);
    }
}

/// <inheritdoc />
internal class UpdateAwardTypeValidator : AbstractValidator<UpdateAwardType>
{
    public UpdateAwardTypeValidator()
    {
        RuleFor(t => t.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(t => t.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(CatalogFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long)
            .When(t => t.Title != null);

        RuleFor(t => t.Description)
            .MaximumLength(CatalogFieldLimits.DescriptionMaxLength).WithMessage(ValidationError.Long)
            .When(t => t.Description != null);

        RuleFor(t => t.Tier)
            .InclusiveBetween(TierScale.MinTier, TierScale.MaxTier).WithMessage(ValidationError.Invalid)
            .When(t => t.Tier.HasValue);

        RuleFor(t => t.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage(ValidationError.Invalid)
            .When(t => t.SortOrder.HasValue);
    }
}

/// <inheritdoc />
internal class CreateContestSeriesValidator : AbstractValidator<CreateContestSeries>
{
    public CreateContestSeriesValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(s => s.ContestType)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        RuleFor(s => s.Number)
            .GreaterThan(0).WithMessage(ValidationError.Invalid);

        RuleFor(s => s.Year)
            .GreaterThanOrEqualTo(ContestSeries.MinYear).WithMessage(ValidationError.Invalid)
            .LessThanOrEqualTo(_ => ContestSeries.LatestYear(dateTimeProvider.Now)).WithMessage(ValidationError.Invalid);

        RuleFor(s => s.TopicUrl)
            .MaximumLength(500).WithMessage(ValidationError.Long)
            .When(s => s.TopicUrl != null);
    }
}

/// <inheritdoc />
internal class UpdateContestSeriesValidator : AbstractValidator<UpdateContestSeries>
{
    public UpdateContestSeriesValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(s => s.Id)
            .NotEmpty().WithMessage(ValidationError.Empty);

        RuleFor(s => s.Number)
            .GreaterThan(0).WithMessage(ValidationError.Invalid)
            .When(s => s.Number.HasValue);

        RuleFor(s => s.Year)
            .GreaterThanOrEqualTo(ContestSeries.MinYear).WithMessage(ValidationError.Invalid)
            .LessThanOrEqualTo(_ => ContestSeries.LatestYear(dateTimeProvider.Now))
            .WithMessage(ValidationError.Invalid)
            .When(s => s.Year.HasValue);

        RuleFor(s => s.TopicUrl)
            .MaximumLength(500).WithMessage(ValidationError.Long)
            .When(s => s.TopicUrl != null);
    }
}
