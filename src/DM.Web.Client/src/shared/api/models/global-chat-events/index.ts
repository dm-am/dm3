import type { User } from "../common";

type GlobalChatEventStatus = "Scheduled" | "Live" | "Ended";

export type GlobalChatEventSummary = {
  id: string;
  title: string;
  startsUtc: string;
  status: GlobalChatEventStatus;
  isOpen: boolean;
  participantCount: number;
};

type GlobalChatEventParticipant = {
  id: string;
  user: User;
  isOrganizer: boolean;
  joinedUtc: string;
};

export type GlobalChatEvent = {
  id: string;
  title: string;
  description: string;
  startsUtc: string;
  /** Actual start, omitted until started: the start is manual, so a countdown on startsUtc lies. */
  startedUtc?: string;
  /** Actual end. Omitted while the event is still running. */
  endedUtc?: string;
  duration: string | null;
  isOpen: boolean;
  status: GlobalChatEventStatus;
  createdBy: User;
  participants: GlobalChatEventParticipant[];
};
