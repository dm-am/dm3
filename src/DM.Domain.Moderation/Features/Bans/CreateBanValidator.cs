using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation;
using DM.Domain.Moderation.Features.Warnings;

namespace DM.Domain.Moderation.Features.Bans;

internal class CreateBanValidator : AbstractValidator<CreateBan>
{
    public CreateBanValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(b => b.Username)
            .NotEmpty().WithMessage(ValidationError.Empty);

        // The Comment surface does not declare [private]: the tag would hide nothing there.
        RuleFor(b => b.Comment)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(WarningPolicy.ReasonMaxLength).WithMessage(ValidationError.Long)
            .Must(comment => !PrivateBlockMarkup.ContainsPrivateMarkup(comment))
            .WithMessage(b => PrivateBlockMarkup.DescribeSurfaceRefusal(b.Comment));

        RuleFor(b => b.AccessRestrictionPolicy)
            .Must(policy => policy is AccessPolicy.DemocraticBan or AccessPolicy.FullBan)
            .WithMessage(ValidationError.Invalid);

        RuleFor(b => b.DurationHours)
            .GreaterThan(0).WithMessage(ValidationError.Invalid);

        RuleFor(b => b.ExpiresUtc)
            .GreaterThan(_ => dateTimeProvider.Now).WithMessage(ValidationError.Invalid);

        RuleFor(b => b)
            .Must(b => b.DurationHours.HasValue || b.ExpiresUtc.HasValue || b.IsVoluntary)
            .WithMessage("Для бана укажите срок или дату окончания")
            .WithName("Duration");
    }
}
