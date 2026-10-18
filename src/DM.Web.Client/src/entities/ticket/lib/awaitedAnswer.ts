import type { TicketStatus } from "../api";

/**
 * Whether a ticket waits for the answer of the reader: the red "*" after its
 * status, the mark the menu of a game sets by a room that waits for the
 * reader's move (GameRoomLink).
 *
 * A ticket waits for one side at a time, and its status says which. Waiting
 * for the user, it waits for its author, signed in: the guest has no account
 * to be marked on. Waiting for moderation, it waits for every moderator it is
 * shown to, but not for its author when he is a moderator himself: he is its
 * side, and his answer is a reply, not moderation's.
 *
 * `moderates` is what the screen knows of the reader and the ticket: every
 * ticket of a queue of moderation is its reader's to answer (the server leaves
 * out the ones he may not read), on the page of one ticket the server says so
 * (canResolve), and "Мои обращения" holds the reader's own tickets alone.
 */
export function awaitsReader(
  ticket: { status: TicketStatus; reporter?: { id?: string } | null },
  readerId: string | null | undefined,
  moderates: boolean,
): boolean {
  if (!readerId) return false;
  if (ticket.reporter?.id === readerId) {
    return ticket.status === "WaitingForUser";
  }
  return moderates && ticket.status === "WaitingForModeration";
}
