using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using FluentValidation;

namespace DM.Domain.Moderation.Features.Tickets;

/// <summary>
/// Validator for an answer in the conversation of a ticket
/// </summary>
internal class CreateTicketCommentValidator : AbstractValidator<CreateTicketComment>
{
    public CreateTicketCommentValidator(IBbCodeNestingLimit nestingLimit)
    {
        // Moderation answers with one of three statuses. Waiting for moderation is
        // the author's turn and comes of his answer alone, never of a choice.
        RuleFor(c => c.Status)
            .Must(status => status is null or TicketStatus.WaitingForUser or TicketStatus.Closed or TicketStatus.Spam)
            .WithMessage(ValidationError.Invalid);

        // An answer that passes the turn to the author has to say something to
        // him. Closing a ticket or marking it as spam is an answer by itself, and
        // the moderator may leave the text empty for it.
        RuleFor(c => c.Text)
            .NotEmpty().WithMessage(ValidationError.Empty)
            .When(c => !EndsTheTicket(c.Status));

        // As long as the text of the ticket itself (TicketTextLimits), not as long
        // as a comment of a topic: the whole conversation comes back with the
        // ticket in one answer.
        RuleFor(c => c.Text)
            .MaximumLength(TicketTextLimits.MaxLength).WithMessage(ValidationError.Long);

        // Text the renderer will refuse is refused here, where the author can
        // still fix it: stored, it renders as nothing. See IBbCodeNestingLimit.
        RuleFor(c => c.Text)
            .Must(nestingLimit.IsWithinLimit).WithMessage(ValidationError.Invalid)
            .When(c => !string.IsNullOrEmpty(c.Text));

        // The conversation renders with the tag set of comments, where a hiding
        // block is not markup: the lines would be published with the tag around
        // them, as in the text of the ticket.
        RuleFor(c => c.Text)
            .Must(text => !PrivateBlockMarkup.ContainsPrivateMarkup(text))
            .WithMessage(c => PrivateBlockMarkup.DescribeSurfaceRefusal(c.Text))
            .When(c => !string.IsNullOrEmpty(c.Text));
    }

    /// <summary>
    /// Whether the status ends the ticket: closed as resolved, or as spam.
    /// </summary>
    private static bool EndsTheTicket(TicketStatus? status) =>
        status is TicketStatus.Closed or TicketStatus.Spam;
}
