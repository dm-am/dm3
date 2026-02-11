import type { Envelope, CursorEnvelope, ListEnvelope } from "@/api/models/common";
import type { GlobalChatMessage } from "@/api/models/globalChat";
import type { GlobalChatEvent, GlobalChatEventSummary } from "@/api/models/globalChatEvents";
import Api from "@/api";
import { BbRenderMode } from "@/api/bbRenderMode";
import { GLOBAL_CHAT_CONVERSATION_ID } from "@/constants/globalChat";

/**
 * Query parameters for cursor-based pagination
 */
export type CursorQuery = {
  cursor?: string;
  aroundMessageId?: string;
  nearTimestampUtc?: string;
  limit?: number;
};

/**
 * Global Chat API
 *
 * Uses the well-known global chat conversation ID directly,
 * avoiding an extra HTTP request to fetch it.
 */
export default new (class GlobalChatApi {
  // ─────────────────────────────────────────────────────────────
  // Messages (via unified conversations endpoint)
  // ─────────────────────────────────────────────────────────────

  /**
   * Get messages with cursor-based pagination
   */
  public getMessages(query: CursorQuery = {}) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>(
      `conversations/${GLOBAL_CHAT_CONVERSATION_ID}/messages`,
      {
        cursor: query.cursor,
        aroundMessageId: query.aroundMessageId,
        nearTimestampUtc: query.nearTimestampUtc,
        limit: query.limit ?? 50,
      },
    );
  }

  /**
   * Get messages before the cursor (older)
   */
  public getMessagesBefore(cursor: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>(
      `conversations/${GLOBAL_CHAT_CONVERSATION_ID}/messages`,
      { cursor, limit },
    );
  }

  /**
   * Get messages after the cursor (newer)
   */
  public getMessagesAfter(cursor: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>(
      `conversations/${GLOBAL_CHAT_CONVERSATION_ID}/messages`,
      { cursor, limit },
    );
  }

  /**
   * Get messages around a specific message
   */
  public getMessagesAround(messageId: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>(
      `conversations/${GLOBAL_CHAT_CONVERSATION_ID}/messages`,
      { aroundMessageId: messageId, limit },
    );
  }

  /**
   * Get messages near a specific timestamp (for date navigation)
   */
  public getMessagesNearDate(timestampUtc: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>(
      `conversations/${GLOBAL_CHAT_CONVERSATION_ID}/messages`,
      { nearTimestampUtc: timestampUtc, limit },
    );
  }

  /**
   * Send a message to global chat
   */
  public sendMessage(text: string) {
    return Api.post<Envelope<GlobalChatMessage>>(
      `conversations/${GLOBAL_CHAT_CONVERSATION_ID}/messages`,
      { text },
    );
  }

  // ─────────────────────────────────────────────────────────────
  // Single message operations (via unified messages endpoint)
  // ─────────────────────────────────────────────────────────────

  /**
   * Get a single message by ID
   */
  public getMessage(id: string) {
    return Api.get<Envelope<GlobalChatMessage>>(`messages/${id}`);
  }

  /**
   * Get message with BBCode text for editing
   */
  public getMessageForEdit(id: string) {
    return Api.get<Envelope<GlobalChatMessage>>(
      `messages/${id}`,
      undefined,
      BbRenderMode.Bb,
    );
  }

  /**
   * Update a message
   */
  public updateMessage(id: string, text: string) {
    return Api.patch<Envelope<GlobalChatMessage>>(`messages/${id}`, { text });
  }

  /**
   * Delete a message (soft delete)
   */
  public deleteMessage(id: string) {
    return Api.delete(`messages/${id}`);
  }

  /**
   * Like a message
   */
  public likeMessage(id: string) {
    return Api.post<Envelope<GlobalChatMessage>>(`messages/${id}/likes`);
  }

  /**
   * Unlike a message
   */
  public unlikeMessage(id: string) {
    return Api.delete(`messages/${id}/likes`);
  }

  // ─────────────────────────────────────────────────────────────
  // Events (dedicated globalchat/events endpoints)
  // ─────────────────────────────────────────────────────────────

  /**
   * Get currently active (Live) event
   */
  public getActiveEvent() {
    return Api.get<Envelope<GlobalChatEventSummary | null>>("globalchat/events/active");
  }

  /**
   * Get list of events (upcoming + live)
   */
  public getEvents() {
    return Api.get<ListEnvelope<GlobalChatEventSummary>>("globalchat/events");
  }

  /**
   * Get event details
   */
  public getEvent(id: string) {
    return Api.get<Envelope<GlobalChatEvent>>(`globalchat/events/${id}`);
  }

  /**
   * Create a new event (requires SeniorModerator+)
   */
  public createEvent(data: {
    title: string;
    description?: string;
    startsAt: string;
    duration?: string;
    isOpen: boolean;
  }) {
    return Api.post<Envelope<GlobalChatEvent>>("globalchat/events", data);
  }

  /**
   * Update an event (organizer only)
   */
  public updateEvent(
    id: string,
    data: {
      title?: string;
      description?: string;
      startsAt?: string;
      duration?: string;
      isOpen?: boolean;
    },
  ) {
    return Api.patch<Envelope<GlobalChatEvent>>(`globalchat/events/${id}`, data);
  }

  /**
   * Delete an event (organizer only)
   */
  public deleteEvent(id: string) {
    return Api.delete(`globalchat/events/${id}`);
  }

  /**
   * Start an event (organizer only)
   */
  public startEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(`globalchat/events/${id}/start`);
  }

  /**
   * End an event (organizer only)
   */
  public endEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(`globalchat/events/${id}/end`);
  }

  /**
   * Join an open event
   */
  public joinEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(`globalchat/events/${id}/join`);
  }

  /**
   * Leave an event
   */
  public leaveEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(`globalchat/events/${id}/leave`);
  }

  /**
   * Add participant to closed event (organizer only)
   */
  public addEventParticipant(eventId: string, login: string) {
    return Api.post<Envelope<GlobalChatEvent>>(
      `globalchat/events/${eventId}/participants`,
      { login },
    );
  }

  /**
   * Remove participant from event (organizer only)
   */
  public removeEventParticipant(eventId: string, userId: string) {
    return Api.delete(`globalchat/events/${eventId}/participants/${userId}`);
  }
})();
