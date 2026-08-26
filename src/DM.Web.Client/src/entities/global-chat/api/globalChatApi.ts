import type {
  CursorEnvelope,
  Envelope,
  ListEnvelope,
  QuoteSource,
} from "@/shared/api/models/common";
import type {
  GlobalChatMessage,
  GlobalChatEvent,
  GlobalChatEventSummary,
} from "../model/types";
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

/**
 * Global Chat API
 *
 * Uses dedicated /global-chat endpoint for messages.
 */
export default new (class GlobalChatApi {
  // ─────────────────────────────────────────────────────────────
  // Messages (dedicated global-chat endpoint)
  // ─────────────────────────────────────────────────────────────

  /**
   * Get messages with cursor-based pagination
   */
  public getMessages(query: CursorQuery = {}) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>("global-chat/messages", {
      cursor: query.cursor,
      aroundMessageId: query.aroundMessageId,
      nearTimestampUtc: query.nearTimestampUtc,
      limit: query.limit ?? 50,
    });
  }

  /**
   * Get messages before the cursor (older)
   */
  public getMessagesBefore(cursor: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>("global-chat/messages", {
      cursor,
      limit,
    });
  }

  /**
   * Get messages after the cursor (newer)
   */
  public getMessagesAfter(cursor: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>("global-chat/messages", {
      cursor,
      limit,
    });
  }

  /**
   * Get messages around a specific message
   */
  public getMessagesAround(messageId: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>("global-chat/messages", {
      aroundMessageId: messageId,
      limit,
    });
  }

  /**
   * Get messages near a specific timestamp (for date navigation)
   */
  public getMessagesNearDate(timestampUtc: string, limit: number = 50) {
    return Api.get<CursorEnvelope<GlobalChatMessage>>("global-chat/messages", {
      nearTimestampUtc: timestampUtc,
      limit,
    });
  }

  /**
   * Send a message to global chat
   */
  public sendMessage(text: string) {
    return Api.post<Envelope<GlobalChatMessage>>("global-chat/messages", {
      text,
    });
  }

  // ─────────────────────────────────────────────────────────────
  // Single message operations
  //
  // Addressed under global-chat, not under the shared messages endpoint.
  // That one belongs to private correspondence and reads a message only for
  // a participant of its chat; the global chat has no participants, because
  // the right to read it belongs to everyone, so every one of these answered
  // 404 — on the reader's own line included.
  // ─────────────────────────────────────────────────────────────

  /**
   * Get a single message by ID
   */
  public getMessage(id: string) {
    return Api.get<Envelope<GlobalChatMessage>>(`global-chat/messages/${id}`);
  }

  /**
   * Get message with round-trip markup for editing
   */
  public getMessageForEdit(id: string) {
    return Api.get<Envelope<GlobalChatMessage>>(
      `global-chat/messages/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  /**
   * Fetch the markup of a quotation of a global chat message.
   *
   * The server composes the whole tag, author included, already filtered for
   * whoever is asking. The client does not build one out of the rendered page:
   * that conversion is lossy, and the source of somebody else's message is not
   * something the browser holds.
   *
   * Addressed under global-chat, for the reason the neighbours above are: the
   * private-message endpoint composes the quotation from the read that asks
   * whether the reader takes part in the chat, and the global chat has no
   * participants, so it answered 404 to everyone.
   */
  public getMessageQuote(id: string) {
    return Api.get<Envelope<QuoteSource>>(`global-chat/messages/${id}/quote`);
  }

  /**
   * Update a message
   */
  public updateMessage(id: string, text: string) {
    return Api.patch<Envelope<GlobalChatMessage>>(
      `global-chat/messages/${id}`,
      { text },
    );
  }

  /**
   * Delete a message (soft delete)
   */
  public deleteMessage(id: string) {
    return Api.delete(`global-chat/messages/${id}`);
  }

  /**
   * Like a message
   */
  public likeMessage(id: string) {
    return Api.post<Envelope<GlobalChatMessage>>(
      `global-chat/messages/${id}/likes`,
    );
  }

  /**
   * Unlike a message
   */
  public unlikeMessage(id: string) {
    return Api.delete(`global-chat/messages/${id}/likes`);
  }

  // ─────────────────────────────────────────────────────────────
  // Events (dedicated global-chat/events endpoints)
  // ─────────────────────────────────────────────────────────────

  /**
   * Get currently active (Live) event.
   * Backend returns an Envelope ({ resource }) or null when no event is live.
   */
  public getActiveEvent() {
    return Api.get<Envelope<GlobalChatEventSummary> | null>(
      "global-chat/events/active",
    );
  }

  /**
   * Get list of events (upcoming + live)
   */
  public getEvents() {
    return Api.get<ListEnvelope<GlobalChatEventSummary>>("global-chat/events");
  }

  /**
   * Get event details.
   * Backend wraps the resource in an Envelope ({ resource }).
   */
  public getEvent(id: string) {
    return Api.get<Envelope<GlobalChatEvent>>(`global-chat/events/${id}`);
  }

  /**
   * Create a new event (requires SeniorModerator+)
   */
  public createEvent(data: {
    title: string;
    description?: string;
    startsUtc: string;
    duration?: string;
    isOpen: boolean;
  }) {
    return Api.post<Envelope<GlobalChatEvent>>("global-chat/events", data);
  }

  /**
   * Update an event (organizer only)
   */
  public updateEvent(
    id: string,
    data: {
      title?: string;
      description?: string;
      startsUtc?: string;
      duration?: string;
      isOpen?: boolean;
    },
  ) {
    return Api.patch<Envelope<GlobalChatEvent>>(
      `global-chat/events/${id}`,
      data,
    );
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
    return Api.post<Envelope<GlobalChatEvent>>(
      `global-chat/events/${id}/start`,
    );
  }

  /**
   * End an event (organizer only)
   */
  public endEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(`global-chat/events/${id}/end`);
  }

  /**
   * Join an open event
   */
  public joinEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(`global-chat/events/${id}/join`);
  }

  /**
   * Leave an event
   */
  public leaveEvent(id: string) {
    return Api.post<Envelope<GlobalChatEvent>>(
      `global-chat/events/${id}/leave`,
    );
  }

  /**
   * Add participant to closed event (organizer only)
   */
  public addEventParticipant(eventId: string, username: string) {
    return Api.post<Envelope<GlobalChatEvent>>(
      `global-chat/events/${eventId}/participants`,
      { username },
    );
  }

  /**
   * Remove participant from event (organizer only).
   * The backend route addresses participants by username (login), same as
   * other per-user resources (e.g. users/me/blacklist/{username}).
   */
  public removeEventParticipant(eventId: string, username: string) {
    return Api.delete(
      `global-chat/events/${eventId}/participants/${encodeURIComponent(username)}`,
    );
  }
})();
