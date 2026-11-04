import type { TicketStatus } from "../api";

// A ticket waits for one side, which its status names; an author who moderates is still its side.
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
