import type { User } from "../common";

/**
 * Global chat event status
 */
export type GlobalChatEventStatus = "Scheduled" | "Live" | "Ended";

/**
 * Global chat event summary (for lists)
 */
export type GlobalChatEventSummary = {
  id: string;
  title: string;
  startsUtc: string;
  status: GlobalChatEventStatus;
  isOpen: boolean;
  participantCount: number;
};

/**
 * Global chat event participant
 */
export type GlobalChatEventParticipant = {
  id: string;
  user: User;
  isOrganizer: boolean;
  joinedUtc: string;
};

/**
 * Full global chat event details
 */
export type GlobalChatEvent = {
  id: string;
  title: string;
  description: string;
  startsUtc: string;
  /** Actual start. Omitted until the event is started: the start is manual and
   * need not fall on startsUtc, so a countdown built on startsUtc plus the
   * duration lies about every event that went live late. */
  startedUtc?: string;
  /** Actual end. Omitted while the event is still running. */
  endedUtc?: string;
  duration: string | null;
  isOpen: boolean;
  status: GlobalChatEventStatus;
  createdBy: User;
  participants: GlobalChatEventParticipant[];
};

/**
 * Input for creating a global chat event
 */
export type CreateGlobalChatEventInput = {
  title: string;
  description?: string;
  startsUtc: string;
  duration?: string;
  isOpen: boolean;
};

/**
 * Input for updating a global chat event
 */
export type UpdateGlobalChatEventInput = {
  title?: string;
  description?: string;
  startsUtc?: string;
  duration?: string;
  isOpen?: boolean;
};
