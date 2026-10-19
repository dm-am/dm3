import type {
  CursorEnvelope,
  Envelope,
  ListEnvelope,
} from "@/shared/api/models/common";
import type {
  GlobalChatMessage,
  GlobalChatEvent,
  GlobalChatEventSummary,
} from "../model/types";
import { Api, RENDER_AUDIENCE } from "@/shared/api";

/** The page the server is asked for when the caller names none. */
const LIMIT = 50;

type CursorQuery = {
  cursor?: string;
  aroundMessageId?: string;
  nearTimestampUtc?: string;
  limit?: number;
};

export default new (class GlobalChatApi {
  public getMessages(query: CursorQuery = {}) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>("global-chat/messages", {
      cursor: query.cursor,
      aroundMessageId: query.aroundMessageId,
      nearTimestampUtc: query.nearTimestampUtc,
      limit: query.limit ?? LIMIT,
    });
  }

  public getMessagesBefore(cursor: string, limit = LIMIT) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>("global-chat/messages", {
      cursor,
      limit,
    });
  }

  public getMessagesAfter(cursor: string, limit = LIMIT) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>("global-chat/messages", {
      cursor,
      limit,
    });
  }

  public getMessagesAround(messageId: string, limit = LIMIT) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>("global-chat/messages", {
      aroundMessageId: messageId,
      limit,
    });
  }

  public getMessagesNearDate(timestampUtc: string, limit = LIMIT) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>("global-chat/messages", {
      nearTimestampUtc: timestampUtc,
      limit,
    });
  }

  public sendMessage(text: string) {
    return Api.post<Envelope<GlobalChatMessage>>("global-chat/messages", {
      text,
    });
  }

  // Under global-chat: the shared messages endpoint reads only for the participants of a chat.
  public getMessage(id: string) {
    return Api.get<Envelope<GlobalChatMessage>>(`global-chat/messages/${id}`);
  }

  public getMessageForEdit(id: string) {
    return Api.get<Envelope<GlobalChatMessage>>(
      `global-chat/messages/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  public updateMessage(id: string, text: string) {
    return Api.patch<Envelope<GlobalChatMessage>>(
      `global-chat/messages/${id}`,
      { text },
    );
  }

  public deleteMessage(id: string) {
    return Api.delete(`global-chat/messages/${id}`);
  }

  public likeMessage(id: string) {
    return Api.post<Envelope<GlobalChatMessage>>(
      `global-chat/messages/${id}/likes`,
    );
  }

  public unlikeMessage(id: string) {
    return Api.delete(`global-chat/messages/${id}/likes`);
  }

  /** Null when no event is live. */

  /** The upcoming and the live ones. */
  public getEvents() {
    return Api.get<ListEnvelope<GlobalChatEventSummary>>("global-chat/events");
  }

  public getEvent(id: string) {
    return Api.get<Envelope<GlobalChatEvent>>(`global-chat/events/${id}`);
  }

  public startEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(
      `global-chat/events/${id}/start`,
    );
  }

  public endEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(`global-chat/events/${id}/end`);
  }

  public joinEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(`global-chat/events/${id}/join`);
  }

  public leaveEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(
      `global-chat/events/${id}/leave`,
    );
  }
})();
