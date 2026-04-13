import type {
  ListEnvelope,
  PagingQuery,
  CursorEnvelope,
  Username,
} from "@/shared/api/models/common";
import type {
  Chat,
  ChatAvailability,
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

  /**
   * Check if current user can start a chat with another user
   * Returns false if either user has blocked the other
   */
  public canStartChat(username: Username) {
    return Api.get<ChatAvailability>(
      `chats/can-start/${encodeURIComponent(username)}`,
    );
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
    return Api.post<Message>(`chats/${chatId}/messages`, { text });
  }

  public getMessage(id: MessageId) {
    return Api.get<Message>(`messages/${id}`);
  }

  /**
   * Get message with BBCode text for editing
   */
  public getMessageForEdit(id: MessageId) {
    return Api.get<Message>(`messages/${id}`, undefined, RENDER_AUDIENCE.AuthorEdit);
  }

  public updateMessage(id: MessageId, message: Patch<Message>) {
    return Api.patch<Message>(`messages/${id}`, message);
  }

  public deleteMessage(id: MessageId) {
    return Api.delete(`messages/${id}`);
  }

  // Message likes
  public likeMessage(id: MessageId) {
    return Api.post<Message>(`messages/${id}/likes`);
  }

  public unlikeMessage(id: MessageId) {
    return Api.delete(`messages/${id}/likes`);
  }
})();
