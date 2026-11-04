import type {
  Ticket,
  TicketComment,
  TicketDetails,
  TrackedTicket,
} from "@/entities/ticket";

/** What the block of a ticket reads: every wire shape of a ticket fits it as it comes. */
export type TicketView = Pick<
  TrackedTicket,
  "status" | "subtype" | "description" | "createdUtc"
> &
  Partial<Pick<Ticket, "reporter" | "isGuest" | "commentsCount">> & {
    /** The conversation, where the page holds it. */
    comments?: TicketComment[];
  };

/** What the conversation reads; the guest is never moderation and is not sent the flag. */
export type ConversationTicket = Pick<
  TrackedTicket,
  "status" | "resolvedUtc" | "resolvedByUsername" | "comments" | "canComment"
> &
  Partial<Pick<TicketDetails, "canResolve">>;
