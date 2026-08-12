import type { Served } from "@/shared/api/models";
import type {
  User,
  ChatId as BaseChatId,
  MessageId as BaseMessageId,
  ChatType as BaseChatType,
  Message as BaseMessage,
  MessageEdit as BaseMessageEdit,
} from "@/shared/api/models/common";

// Message and MessageEdit are declared in shared because entities/global-chat
// needs the same shape and an entity may not import another entity — the header
// of shared/api/models/common/message.ts says so. The ids and the chat type sit
// in that module next to them; Chat below is this slice's own.
export type ChatId = BaseChatId;
export type MessageId = BaseMessageId;
export type ChatType = BaseChatType;
export type Message = BaseMessage;
export type MessageEdit = BaseMessageEdit;

export type Chat = {
  id: Served<ChatId>;
  type: Served<ChatType>;
  participants: Served<User[]>;
  unreadMessagesCount: Served<number>;
  lastMessage?: Served<Message>;
  title?: Served<string | null>;
};

/**
 * Input for creating a group chat
 */
export type CreateChat = {
  title: string;
  participantIds: string[];
};

/**
 * Input for updating a chat (title and/or participants)
 */
export type UpdateChat = {
  title?: string;
  addParticipants?: string[];
  removeParticipants?: string[];
};

/**
 * Result of checking if a chat can be started with another user
 */
export type ChatAvailability = {
  canStart: boolean;
  reason?: "YouBlockedThem" | "CannotCommunicate" | null;
};
