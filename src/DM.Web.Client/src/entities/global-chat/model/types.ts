// Global chat message models - re-exported from unified messaging module
// Note: GlobalChatMessage is now just an alias for Message

import type { Message } from "@/shared/api/models/common";

export type GlobalChatMessage = Message;

// Event types live in shared/api/models (FSD: shared must not import
// entities); the entity re-exports them as its public model surface.
export type {
  GlobalChatEventStatus,
  GlobalChatEventSummary,
  GlobalChatEventParticipant,
  GlobalChatEvent,
  CreateGlobalChatEventInput,
  UpdateGlobalChatEventInput,
} from "@/shared/api/models/global-chat-events";
