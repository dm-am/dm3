/**
 * The longest text of a ticket or of a comment in its conversation.
 *
 * The server holds the same number in TicketTextLimits.MaxLength and refuses a
 * longer one, both at the form that files a ticket and at the form of its
 * conversation. It is shorter than a comment of a topic (BODY_TEXT_MAX_LENGTH):
 * the conversation comes back whole with the ticket, a hundred comments at
 * most. The browser cannot read the server's number, so the form keeps this
 * copy and shows the count against it, and an architecture test compares the
 * two. Shown and not enforced, as the comment limit is: the refusal belongs to
 * the save.
 */
export const TICKET_TEXT_MAX_LENGTH = 10000;
