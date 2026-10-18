using System;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Validator for ticket creation from the public intake forms
/// </summary>
internal class CreateTicketIntakeValidator : AbstractValidator<CreateTicketIntake>
{
    public CreateTicketIntakeValidator(IIdentityProvider identityProvider, IBbCodeNestingLimit nestingLimit)
    {
        RuleFor(t => t.Subtype)
            .IsInEnum().WithMessage(ValidationError.Invalid);

        // Suggestions are discussed on the forum, not filed as tickets. The
        // category stays in the enumeration so the moderation queues keep their
        // filter; nothing may create a ticket of it any more.
        RuleFor(t => t.Subtype)
            .NotEqual(TicketSubtype.SiteImprovementSuggestion).WithMessage(ValidationError.Invalid);

        // The forms do not ask for a subject: the reason and the text say it,
        // and every screen that reads a ticket back names it by its reason when
        // the subject is empty. One sent by hand is still held to its length.
        RuleFor(t => t.Subject)
            .MaximumLength(200).WithMessage(ValidationError.Long)
            .When(t => t.Subject != null);

        // One length for the text of a ticket and for every comment of its
        // conversation (TicketTextLimits).
        RuleFor(t => t.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .MaximumLength(TicketTextLimits.MaxLength).WithMessage(ValidationError.Long);

        // The text is written in the site markup and rendered for moderation,
        // so a text the renderer refuses is refused here, where the author can
        // still fix it: stored, it would render as nothing. See
        // IBbCodeNestingLimit.
        RuleFor(t => t.Text)
            .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid);

        // The ticket renders with the tag set of profile texts, where a hiding
        // block is shown to nobody: the lines the author meant for moderation
        // would reach none of its readers.
        RuleFor(t => t.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(t => PrivateBlockMarkup.DescribeSurfaceRefusal(t.Text));

        // Guests have no account to reach them by, so a contact email is
        // required and must be a valid address — without it the moderation
        // response and the tracking flow have nowhere to land. Authenticated
        // authors are reachable by identity, so their contact stays optional.
        When(_ => !identityProvider.Current.User.IsAuthenticated, () =>
        {
            RuleFor(t => t.Contact)
                .NotEmpty().WithMessage(ValidationError.Empty)
                .EmailAddress().WithMessage(ValidationError.Invalid)
                .MaximumLength(200).WithMessage(ValidationError.Long);
        }).Otherwise(() =>
        {
            RuleFor(t => t.Contact)
                .MaximumLength(200).WithMessage(ValidationError.Long)
                .When(t => t.Contact != null);
        });

        RuleFor(t => t.ViolationUrl)
            .MaximumLength(500).WithMessage(ValidationError.Long)
            .When(t => t.ViolationUrl != null);

        // Two reasons are filed from an account and from nowhere else. A
        // complaint about a person is answered by a warning or a ban, and those
        // go to a reporter the moderator can write back to, as on DM2, where a
        // guest read "Для подачи жалобы требуется авторизация". A request to
        // remove an account is honoured for the owner, and a guest's request
        // cannot say whose it is. Whoever cannot sign in has a reason of his
        // own for it: access recovery, or the complaint about a senior
        // moderator's decision for a full ban.
        RuleFor(t => t.Subtype)
            .Must(subtype => subtype is not (TicketSubtype.UserComplaint or TicketSubtype.AccountRemoval))
            .When(_ => !identityProvider.Current.User.IsAuthenticated)
            .WithMessage(SignInForThisReason);

        // A complaint about a person names the person and the place: without
        // them the moderator starts by searching, and the card of the ticket
        // offers no warning and no ban, both of which need the violator.
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

    /// <summary>
    /// The refusal of a reason that is filed from an account, under the picker.
    /// </summary>
    internal const string SignInForThisReason = "Войдите в аккаунт, чтобы выбрать эту причину";

    /// <summary>
    /// An absolute http or https address: the link a moderator opens from the
    /// ticket, so anything else would lead nowhere.
    /// </summary>
    private static bool IsWebAddress(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var address) &&
        (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);
}
