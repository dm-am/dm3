import type {
  ChatUnreadCount,
  ListEnvelope,
  PagingQuery,
  CursorEnvelope,
  Envelope,
  Username,
} from "@/shared/api/models/common";
import type {
  Chat,
  ChatId,
  CreateChat,
  Message,
  MessageId,
  UpdateChat,
} from "../model/types";
import type { Patch } from "@/shared/api/models";
import { Api } from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api";

/**
 * A screen that shows the refusal of its own request (a window that stays
 * open with the reason in it) says so, and the client keeps the plate down.
 */
type RefusalOptions = {
  /** The screen shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The screen shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
};

/**
 * Query parameters for cursor-based pagination
 */
export type CursorQuery = {
  cursor?: string;
  aroundMessageId?: string;
  nearTimestampUtc?: string;
  limit?: number;
};

export default new (class MessagingApi {
  // Chats
  public getChats(q: PagingQuery) {
    // Convert page number to skip/take for backend
    const pageSize = q.take ?? 20;
    const queryParams: Record<string, number | undefined> = {
      take: pageSize,
    };

    if (q.number && q.number > 1) {
      queryParams.skip = (q.number - 1) * pageSize;
    } else if (q.skip) {
      queryParams.skip = q.skip;
    }

    return Api.get<ListEnvelope<Chat>>("chats", queryParams);
  }

  public async getOrCreateDirectChat(username: Username) {
    // POST creates chat if it doesn't exist, returns existing if present
    return Api.post<Chat>(`chats/direct/${username}`);
  }

  public getChat(id: ChatId) {
    return Api.get<Chat>(`chats/${id}`);
  }

  public markAsRead(id: ChatId) {
    return Api.delete(`chats/${id}/messages/unread`);
  }

  /**
   * The reader's total of unread messages over every conversation they are in.
   *
   * The header badge used to add up the `unreadMessagesCount` of the first page
   * of the chat list, so conversations past that page were missing from the
   * number, and each recount fetched a page of chats with their participants
   * and last messages to read one integer out of it.
   */
  public getUnreadCount() {
    return Api.get<Envelope<ChatUnreadCount>>("chats/unread");
  }

  /**
   * Create a new group chat
   */
  public createChat(input: CreateChat) {
    return Api.post<Chat>("chats", input);
  }

  /**
   * Update an existing chat (title and/or participants)
   */
  public updateChat(id: ChatId, input: UpdateChat) {
    return Api.patch<Chat>(`chats/${id}`, input);
  }

  /**
   * Leave a group chat. It goes out of the reader's list; the others keep it
   * with every message.
   */
  public leaveChat(id: ChatId, options?: RefusalOptions) {
    return Api.post(`chats/${id}/leave`, undefined, options);
  }

  /**
   * Clear the history of a chat for the reader alone: the messages written up
   * to now are no longer given to him, the others keep them.
   */
  public clearHistory(id: ChatId, options?: RefusalOptions) {
    return Api.post(`chats/${id}/clear-history`, undefined, options);
  }

  // Messages
  /**
   * Get messages with cursor-based pagination
   */
  public getMessages(chatId: ChatId, query: CursorQuery = {}) {
    return Api.get<CursorEnvelope<Message>>(`chats/${chatId}/messages`, {
      cursor: query.cursor,
      aroundMessageId: query.aroundMessageId,
      nearTimestampUtc: query.nearTimestampUtc,
      limit: query.limit ?? 50,
    });
  }

  /**
   * Get messages before the cursor (older)
   */
  public getMessagesBefore(chatId: ChatId, cursor: string, limit: number = 50) {
    return Api.get<CursorEnvelope<Message>>(`chats/${chatId}/messages`, {
      cursor,
      limit,
    });
  }

  /**
   * Get messages after the cursor (newer)
   */
  public getMessagesAfter(chatId: ChatId, cursor: string, limit: number = 50) {
    return Api.get<CursorEnvelope<Message>>(`chats/${chatId}/messages`, {
      cursor,
      limit,
    });
  }

  public sendMessage(chatId: ChatId, text: string) {
    return Api.post<Envelope<Message>>(`chats/${chatId}/messages`, { text });
  }

  public getMessage(id: MessageId) {
    return Api.get<Envelope<Message>>(`messages/${id}`);
  }

  /**
   * Get message with BBCode text for editing
   */
  public getMessageForEdit(id: MessageId) {
    return Api.get<Envelope<Message>>(
      `messages/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  public updateMessage(id: MessageId, message: Patch<Message>) {
    return Api.patch<Envelope<Message>>(`messages/${id}`, message);
  }

  public deleteMessage(id: MessageId) {
    return Api.delete(`messages/${id}`);
  }

  // Message likes
  public likeMessage(id: MessageId) {
    return Api.post<Envelope<Message>>(`messages/${id}/likes`);
  }

  public unlikeMessage(id: MessageId) {
    return Api.delete(`messages/${id}/likes`);
  }
})();
