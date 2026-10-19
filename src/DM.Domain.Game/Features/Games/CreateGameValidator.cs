using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Content;
using DM.Domain.Core.Users;
using DM.Domain.Game.Features.AttributeSchemas;
using FluentValidation;

namespace DM.Domain.Game.Features.Games;

internal class CreateGameValidator : AbstractValidator<CreateGame>
{
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

        // The Profile surface declares no [private]: the tag belongs to the posts, not to the game page.
        RuleFor(g => g.Info)
            .Must(info => !PrivateBlockMarkup.ContainsPrivateMarkup(info))
            .WithMessage(g => PrivateBlockMarkup.DescribeSurfaceRefusal(g.Info));

        RuleFor(g => g.CommentsAccessMode)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        // The sheet is created with the game, so a set it cannot hold refuses the whole request.
        this.RuleForSpecifications(g => g.AttributeSpecifications);

        When(g => !string.IsNullOrEmpty(g.AssistantUsername), () =>
            RuleFor(g => g.AssistantUsername)
                .MustAsync(userLookupService.UsernameExistsAsync).WithMessage(ValidationError.Invalid));
    }
}
