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
  /**
   * What the viewer may do in this chat, answered by the code that refuses the
   * request. On the read of one chat and the writes that return one; the list
   * of chats leaves it out.
   */
  viewer?: Served<ChatViewer>;
};

/**
 * What the viewer may do in a chat (mirrors ChatViewer). The server asks each
 * flag with the code that refuses the write it stands for.
 */
export type ChatViewer = {
  /**
   * May write a new message: a participant, not closed off by the one other
   * participant of a conversation of two; in the global chat, signed in, under
   * no ban and not outside a closed event.
   */
  canWrite: Served<boolean>;
  /** May leave the chat: a participant of a group chat. */
  canLeave?: Served<boolean>;
  /** May clear the history for himself: a participant of a direct or group chat. */
  canClearHistory?: Served<boolean>;
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
