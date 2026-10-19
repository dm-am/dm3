import type {
  UnreadCount,
  ListEnvelope,
  PagingQuery,
  CursorEnvelope,
  Envelope,
  Username,
} from "@/shared/api/models/common";
import type { Chat, ChatId, Message, MessageId } from "../model/types";
import type { Patch } from "@/shared/api/models";
import { Api, RENDER_AUDIENCE } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

type CursorQuery = {
  cursor?: string;
  aroundMessageId?: string;
  nearTimestampUtc?: string;
  limit?: number;
};

export default new (class MessagingApi {
  public getChats(q: PagingQuery) {
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

  public getOrCreateDirectChat(username: Username) {
    return Api.post<Chat>(`chats/direct/${username}`);
  }

  public getChat(id: ChatId) {
    return Api.get<Chat>(`chats/${id}`);
  }

  public markAsRead(id: ChatId) {
    return Api.delete(`chats/${id}/messages/unread`);
  }

  /** Over every conversation, not only the first page of the list. */
  public getUnreadCount() {
    return Api.get<Envelope<UnreadCount>>("chats/unread");
  }

  /** The others keep the chat with every message. */
  public leaveChat(id: ChatId, options?: RequestOptions) {
    return Api.post(`chats/${id}/leave`, undefined, options);
  }

  /** For the reader alone; the others keep the messages. */
  public clearHistory(id: ChatId, options?: RequestOptions) {
    return Api.post(`chats/${id}/clear-history`, undefined, options);
  }

  public getMessages(chatId: ChatId, query: CursorQuery = {}) {
    return Api.get<CursorEnvelope<Message>>(`chats/${chatId}/messages`, {
      cursor: query.cursor,
      aroundMessageId: query.aroundMessageId,
      nearTimestampUtc: query.nearTimestampUtc,
      limit: query.limit ?? 50,
    });
  }

  public getMessagesBefore(chatId: ChatId, cursor: string, limit: number = 50) {
    return Api.get<CursorEnvelope<Message>>(`chats/${chatId}/messages`, {
      cursor,
      limit,
    });
  }

  public getMessagesAfter(chatId: ChatId, cursor: string, limit: number = 50) {
    return Api.get<CursorEnvelope<Message>>(`chats/${chatId}/messages`, {
      cursor,
      limit,
    });
  }

  public sendMessage(chatId: ChatId, text: string) {
    return Api.post<Envelope<Message>>(`chats/${chatId}/messages`, { text });
  }

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

  public likeMessage(id: MessageId) {
    return Api.post<Envelope<Message>>(`messages/${id}/likes`);
  }

  public unlikeMessage(id: MessageId) {
    return Api.delete(`messages/${id}/likes`);
  }
})();
