using System;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tickets;

internal class CreateTicketIntakeValidator : AbstractValidator<CreateTicketIntake>
{
    public CreateTicketIntakeValidator(IIdentityProvider identityProvider, IBbCodeNestingLimit nestingLimit)
    {
        RuleFor(t => t.Subtype)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        // The forms ask no subject; one sent by hand is still held to its length.
        RuleFor(t => t.Subject)
            .MaximumLength(TicketTextLimits.SubjectMaxLength).WithMessage(ValidationError.Long);

        RuleFor(t => t.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(TicketTextLimits.MaxLength).WithMessage(ValidationError.Long);

        // Stored, a text the renderer refuses renders as nothing (IBbCodeNestingLimit).
        RuleFor(t => t.Text)
            .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid);

        // The tag set of profile texts shows a hiding block to nobody, moderation included.
        RuleFor(t => t.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(t => PrivateBlockMarkup.DescribeSurfaceRefusal(t.Text));

        // A guest has no account to be answered through, so his contact is a required address.
        When(_ => !identityProvider.Current.User.IsAuthenticated, () =>
        {
            RuleFor(t => t.Contact)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .EmailAddress().WithMessage(RefusalMessage.EmailInvalid);
        });

        RuleFor(t => t.Contact)
            .MaximumLength(TicketTextLimits.ContactMaxLength).WithMessage(ValidationError.Long);

        RuleFor(t => t.ViolationUrl)
            .MaximumLength(TicketTextLimits.ViolationUrlMaxLength).WithMessage(ValidationError.Long);

        // A complaint about a person is answered by a warning or a ban, sent to a reporter
        // moderation can write back to, and an account removal is honoured for its owner.
        // Whoever cannot sign in has the access recovery and the complaint about a full ban.
        RuleFor(t => t.Subtype)
            .Must(subtype => subtype is not (TicketSubtype.UserComplaint or TicketSubtype.AccountRemoval))
            .When(_ => !identityProvider.Current.User.IsAuthenticated)
            .WithMessage(SignInForThisReason);

        // Without the person and the place the card of the ticket offers no warning and no ban.
        When(t => t.Subtype == TicketSubtype.UserComplaint, () =>
        {
            RuleFor(t => t.TargetUsername)
                .NotEmpty().WithMessage(ValidationError.Empty);

            RuleFor(t => t.ViolationUrl)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .Must(IsWebAddress).WithMessage(ValidationError.Invalid)
                .When(t => !string.IsNullOrWhiteSpace(t.ViolationUrl), ApplyConditionTo.CurrentValidator);
        });
    }

    internal const string SignInForThisReason = "Войдите в аккаунт, чтобы выбрать эту причину";

    /// <summary>
    /// An absolute http or https address: the link a moderator opens from the
    /// ticket, so anything else would lead nowhere.
    /// </summary>
    private static bool IsWebAddress(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var address) &&
        (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);
}
