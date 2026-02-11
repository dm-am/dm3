import type { User } from "@/api/models/community";

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
  startsAt: string;
  status: GlobalChatEventStatus;
  isOpen: boolean;
};

/**
 * Global chat event participant
 */
export type GlobalChatEventParticipant = {
  id: string;
  user: User;
  isOrganizer: boolean;
  joinedAt: string;
};

/**
 * Full global chat event details
 */
export type GlobalChatEvent = {
  id: string;
  title: string;
  description: string;
  startsAt: string;
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
  startsAt: string;
  duration?: string;
  isOpen: boolean;
};

/**
 * Input for updating a global chat event
 */
export type UpdateGlobalChatEventInput = {
  title?: string;
  description?: string;
  startsAt?: string;
  duration?: string;
  isOpen?: boolean;
};
