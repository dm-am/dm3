import type {
  Ticket,
  TicketComment,
  TicketDetails,
  TrackedTicket,
} from "@/entities/ticket";

/**
 * What the block of a ticket reads: the fields every wire shape of a ticket
 * has, and those only a signed-in reader is sent. A ticket of a list, the page
 * of one ticket and the guest's ticket read by its token all fit it as they
 * come, so a page hands its ticket over as is.
 */
export type TicketView = Pick<
  TrackedTicket,
  "status" | "subtype" | "descriptionHtml" | "createdUtc"
> &
  Partial<Pick<Ticket, "reporter" | "isGuest" | "commentsCount">> & {
    /** The conversation, where the page holds it. */
    comments?: TicketComment[];
  };

/**
 * What the conversation under the block reads: its answers, whether the
 * reader may write and whether he answers as moderation, and how the ticket
 * ended. The page of one ticket and the guest's ticket fit it as they come;
 * the guest is never moderation and is not sent the flag.
 */
export type ConversationTicket = Pick<
  TrackedTicket,
  "status" | "resolvedUtc" | "resolvedByUsername" | "comments" | "canComment"
> &
  Partial<Pick<TicketDetails, "canResolve">>;
