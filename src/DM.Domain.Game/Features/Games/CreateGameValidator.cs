using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Users;
using FluentValidation;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// Validator for game creation DTO model
/// </summary>
internal class CreateGameValidator : AbstractValidator<CreateGame>
{
    /// <inheritdoc />
    public CreateGameValidator(
        IUserLookupService userLookupService)
    {
        RuleFor(g => g.Title)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(GameFieldLimits.TitleMaxLength).WithMessage(ValidationError.Long);

        RuleFor(g => g.SystemName)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(GameFieldLimits.SystemMaxLength).WithMessage(ValidationError.Long);

        RuleFor(g => g.NarrativeSetting)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(GameFieldLimits.SettingMaxLength).WithMessage(ValidationError.Long);

        RuleFor(g => g.Info)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MinimumLength(GameFieldLimits.InfoMinLength).WithMessage(ValidationError.Short);

        When(g => !string.IsNullOrEmpty(g.AssistantUsername), () =>
            RuleFor(g => g.AssistantUsername)
                .MustAsync(userLookupService.UsernameExistsAsync).WithMessage(ValidationError.Invalid));
    }
}
