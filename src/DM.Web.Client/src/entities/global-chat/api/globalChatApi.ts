import type { CursorEnvelope, ListEnvelope } from "@/shared/api/models/common";
import type { GlobalChatMessage, GlobalChatEvent, GlobalChatEventSummary } from "../model/types";
import { Api } from "@/shared/api";
import { BbRenderMode } from "@/shared/api";
import { GLOBAL_CHAT_ID } from "@/shared/config/globalChat";

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
 * Uses the well-known global chat ID directly,
 * avoiding an extra HTTP request to fetch it.
 */
export default new (class GlobalChatApi {
  // ─────────────────────────────────────────────────────────────
  // Messages (via unified chats endpoint)
  // ─────────────────────────────────────────────────────────────

  /**
   * Get messages with cursor-based pagination
   */
  public getMessages(query: CursorQuery = {}) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>(
      `chats/${GLOBAL_CHAT_ID}/messages`,
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
      `chats/${GLOBAL_CHAT_ID}/messages`,
      { cursor, limit },
    );
  }

  /**
   * Get messages after the cursor (newer)
   */
  public getMessagesAfter(cursor: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>(
      `chats/${GLOBAL_CHAT_ID}/messages`,
      { cursor, limit },
    );
  }

  /**
   * Get messages around a specific message
   */
  public getMessagesAround(messageId: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>(
      `chats/${GLOBAL_CHAT_ID}/messages`,
      { aroundMessageId: messageId, limit },
    );
  }

  /**
   * Get messages near a specific timestamp (for date navigation)
   */
  public getMessagesNearDate(timestampUtc: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>(
      `chats/${GLOBAL_CHAT_ID}/messages`,
      { nearTimestampUtc: timestampUtc, limit },
    );
  }

  /**
   * Send a message to global chat
   */
  public sendMessage(text: string) {
    return Api.post<GlobalChatMessage>(
      `chats/${GLOBAL_CHAT_ID}/messages`,
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
    return Api.get<GlobalChatMessage>(`messages/${id}`);
  }

  /**
   * Get message with BBCode text for editing
   */
  public getMessageForEdit(id: string) {
    return Api.get<GlobalChatMessage>(
      `messages/${id}`,
      undefined,
      BbRenderMode.Bb,
    );
  }

  /**
   * Update a message
   */
  public updateMessage(id: string, text: string) {
    return Api.patch<GlobalChatMessage>(`messages/${id}`, { text });
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
    return Api.post<GlobalChatMessage>(`messages/${id}/likes`);
  }

  /**
   * Unlike a message
   */
  public unlikeMessage(id: string) {
    return Api.delete(`messages/${id}/likes`);
  }

  // ─────────────────────────────────────────────────────────────
  // Events (dedicated global-chat/events endpoints)
  // ─────────────────────────────────────────────────────────────

  /**
   * Get currently active (Live) event
   */
  public getActiveEvent() {
    return Api.get<GlobalChatEventSummary | null>("global-chat/events/active");
  }

  /**
   * Get list of events (upcoming + live)
   */
  public getEvents() {
    return Api.get<ListEnvelope<GlobalChatEventSummary>>("global-chat/events");
  }

  /**
   * Get event details
   */
  public getEvent(id: string) {
    return Api.get<GlobalChatEvent>(`global-chat/events/${id}`);
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
    return Api.post<GlobalChatEvent>("global-chat/events", data);
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
    return Api.patch<GlobalChatEvent>(`global-chat/events/${id}`, data);
  }

  /**
   * Delete an event (organizer only)
   */
  public deleteEvent(id: string) {
    return Api.delete(`global-chat/events/${id}`);
  }

  /**
   * Start an event (organizer only)
   */
  public startEvent(id: string) {
    return Api.post<GlobalChatEvent>(`global-chat/events/${id}/start`);
  }

  /**
   * End an event (organizer only)
   */
  public endEvent(id: string) {
    return Api.post<GlobalChatEvent>(`global-chat/events/${id}/end`);
  }

  /**
   * Join an open event
   */
  public joinEvent(id: string) {
    return Api.post<GlobalChatEvent>(`global-chat/events/${id}/join`);
  }

  /**
   * Leave an event
   */
  public leaveEvent(id: string) {
    return Api.post<GlobalChatEvent>(`global-chat/events/${id}/leave`);
  }

  /**
   * Add participant to closed event (organizer only)
   */
  public addEventParticipant(eventId: string, username: string) {
    return Api.post<GlobalChatEvent>(
      `global-chat/events/${eventId}/participants`,
      { username },
    );
  }

  /**
   * Remove participant from event (organizer only)
   */
  public removeEventParticipant(eventId: string, userId: string) {
    return Api.delete(`global-chat/events/${eventId}/participants/${userId}`);
  }
})();
