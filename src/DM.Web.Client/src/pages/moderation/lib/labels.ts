// Russian display labels for moderation enums. This is the dictionary: the
// server keeps none, so there is nothing here to drift away from.
import type { PremoderationStatus } from "@/entities/moderation";
import type { TicketStatus } from "@/entities/ticket";

// ==================== Ticket status ====================

/**
 * The order of the four statuses a ticket is stored with, as a queue of
 * moderation stands and as its filter "Статус" offers them: the tickets
 * waiting for moderation first (doc 4.2.3.8.7/8), then those waiting for
 * their author, then the ended ones.
 */
export const TICKET_STATUS_ORDER: Record<TicketStatus, number> = {
  WaitingForModeration: 0,
  WaitingForUser: 1,
  Closed: 2,
  Spam: 3,
};

// ==================== Premoderation ====================

/** The wording of PremoderationStatus, and the only copy of it. */
export const PREMODERATION_STATUS_LABELS: Record<
  Exclude<PremoderationStatus, "Approved">,
  string
> = {
  AwaitingApproval: "Ожидает проверки",
  AwaitingEdits: "Требует правок",
};

// ==================== Bans ====================

// The term of a ban is read where the ban record is drawn (entities/moderation,
// warningLabels.ts), and this list of violators reads the same words.
export { banTermLabel } from "@/entities/moderation";
