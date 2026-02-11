// Global chat message models - re-exported from unified messaging module
// Note: GlobalChatMessage is now just an alias for Message

import type { Message, MessageEdit } from "@/api/models/messaging";

export type GlobalChatMessageId = string;
export type GlobalChatMessageEdit = MessageEdit;
export type GlobalChatMessage = Message;
