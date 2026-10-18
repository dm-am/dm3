using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.AttributeSchemas;
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

        // The game description renders on the Profile surface, which declares
        // neither [mod] nor [private]: the tag is not markup there and hides
        // nothing. [private] belongs to the posts inside the game, not to the
        // page that advertises it.
        RuleFor(g => g.Info)
            .Must(info => !PrivateBlockMarkup.ContainsPrivateMarkup(info))
            .WithMessage(g => PrivateBlockMarkup.DescribeSurfaceRefusal(g.Info));

        RuleFor(g => g.CommentsAccessMode)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        // The character sheet is created with the game, so its rules are part of
        // creating one: a set the sheet cannot hold refuses the whole request,
        // and no game is written for it.
        RuleForEach(g => g.AttributeSpecifications)
            .ChildRules(AttributeSpecificationRules.Apply);

        RuleFor(g => g.AttributeSpecifications)
            .Custom(AttributeSpecificationRules.AddFailures);

        When(g => !string.IsNullOrEmpty(g.AssistantUsername), () =>
            RuleFor(g => g.AssistantUsername)
                .MustAsync(userLookupService.UsernameExistsAsync).WithMessage(ValidationError.Invalid));
    }
}
