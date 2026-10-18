import type { Envelope, ListEnvelope, User } from "@/shared/api/models/common";
import { Api, X_DM_TICKET_TOKEN } from "@/shared/api";

/**
 * The page size the wire accepts, and the one it assumes.
 *
 * PagingQuery caps take at 100 ([Range(1, 100)]) while the "Сущностей на
 * странице" preference offers 200, so a caller that forwards the preference
 * verbatim is refused with 400 and the reader gets an error banner where the
 * list was. The clamp lives here, next to the only code that knows the wire
 * contract; 20 is what the server assumes when no page is named, spelled out so
 * the offset is computed from the size actually requested.
 */
const MAX_PAGE_SIZE = 100;
const DEFAULT_PAGE_SIZE = 20;

// ==================== Ticket Intake Types ====================

/** Ticket ("обращение") categories; mirrors backend TicketSubtype */
export type TicketSubtype =
  | "UserComplaint"
  | "ModeratorDecisionComplaint"
  | "SeniorModeratorDecisionComplaint"
  | "SiteImprovementSuggestion"
  | "Bug"
  | "AccessRecovery"
  | "RegistrationIssue"
  | "AccountRemoval"
  | "AccountRestoration"
  | "PersonalData"
  | "AgreementQuestion";

export type CreateTicketIntake = {
  subtype: TicketSubtype;
  /**
   * A short subject line. The support and complaint forms do not ask for one:
   * the reason and the text say it, and the screens that read a ticket back
   * name it by its reason when there is none.
   */
  subject?: string;
  /** The ticket text, in the site markup */
  text: string;
  contact?: string;
  violationUrl?: string;
  targetUsername?: string;
  /** Honeypot field for bot protection - must stay empty */
  website?: string;
};

/**
 * Response of the intake submission. Mirrors backend
 * CreateTicketIntakeResponse: the ticket just filed, which a signed-in author
 * opens at /my-tickets/<id>, and a guest tracking token (null for signed-in
 * authors), which is a guest's only way back to the answer.
 */
export type CreateTicketIntakeResult = {
  ticketId: string;
  trackingToken?: string | null;
};

// ==================== Ticket Read Types ====================

/** Ticket status; mirrors backend TicketStatus (serialized as string) */
export type TicketStatus =
  | "WaitingForModeration"
  | "WaitingForUser"
  | "Closed"
  | "Spam";

/**
 * Ticket ("обращение") as returned by the ticket endpoints.
 * Mirrors backend DM.Web.API.Features.Moderation.Tickets.Ticket DTO.
 *
 * Field semantics for intake-created tickets (the /support and /complaint
 * forms): `comment` holds the short subject line, `description` holds the
 * ticket body text (see TicketService.CreateIntakeTicket).
 *
 * Which fields are actually populated is role-scoped server-side; there is one
 * wire shape, so there is one type. Two identical copies of it used to live in
 * shared/api — one per client — and could drift apart silently.
 */
export type Ticket = {
  id: string;
  /**
   * The reporter: null for a guest submission and for an account removed since;
   * `isGuest` tells the two apart.
   */
  reporter?: User | null;
  /** Whether the ticket was filed without an account */
  isGuest: boolean;
  /**
   * Contact email left by a guest author. For moderation to work with, and not
   * a part of the text: the text is the author's words alone.
   */
  guestEmail?: string | null;
  entityId?: string | null;
  entityType?: string | null;
  status: TicketStatus;
  subtype: TicketSubtype;
  createdUtc: string;
  /**
   * Ticket body as it was written, in the site markup. The fields of the form
   * that are not the text (the violator, the link, the contact of an author
   * with an account) are lines of it, written there by the server at filing.
   */
  description: string;
  /**
   * The body rendered by the server for display. The forms write it in the
   * markup editor, so the screens that read a ticket back show this and not
   * the text above, which would print the tags as brackets.
   */
  descriptionHtml: string;
  /** Short subject line (intake Subject is stored here) */
  comment: string;
  /** How many answers the conversation of the ticket holds */
  commentsCount: number;
  resolvedUtc?: string | null;
};

/**
 * One comment of the conversation of a ticket. Mirrors backend TicketComment.
 * Not edited, not deleted, not liked.
 */
export type TicketComment = {
  id: string;
  /**
   * The author: null for the guest who filed the ticket and for an account
   * removed since; `isGuest` tells the two apart.
   */
  author?: User | null;
  isGuest: boolean;
  createdUtc: string;
  /** The text rendered by the server for display */
  text: string;
};

/**
 * A ticket with its conversation, the page of a ticket. Mirrors backend
 * TicketDetails.
 */
export type TicketDetails = Ticket & {
  /** From the oldest comment to the newest */
  comments: TicketComment[];
  /** Whether the caller may write in the conversation */
  canComment: boolean;
  /**
   * Whether the caller answers as moderation: it is open, and he moderates it
   * without being its author. He then chooses with his answer the status the
   * ticket moves to (AnswerTicketRequest), closing it among them.
   */
  canResolve: boolean;
  /** The moderator who closed the ticket (null while it is open) */
  resolvedByUsername?: string | null;
};

/**
 * Public, token-gated view of a guest ticket (GET /v1/tickets/track, token in
 * the X-Dm-Ticket-Token header).
 * Mirrors backend TrackedTicket — moderation internals are intentionally
 * omitted.
 */
export type TrackedTicket = {
  status: TicketStatus;
  subtype: TicketSubtype;
  /** Short subject line */
  subject: string;
  /** Ticket body as it was written, in the site markup */
  description: string;
  /** The body rendered by the server for display (see Ticket) */
  descriptionHtml: string;
  createdUtc: string;
  resolvedUtc?: string | null;
  /** The moderator who closed the ticket (null while it is open) */
  resolvedByUsername?: string | null;
  /** The conversation, from the oldest comment to the newest */
  comments: TicketComment[];
  /** Whether the guest may write in the conversation */
  canComment: boolean;
};

/**
 * The statuses moderation moves a ticket to with its answer. Waiting for
 * moderation is not one of them: it comes of the author's answer alone.
 */
export type AnswerStatus = Extract<
  TicketStatus,
  "WaitingForUser" | "Closed" | "Spam"
>;

/**
 * An answer in the conversation of a ticket. Mirrors backend
 * AnswerTicketRequest. The author sends the text alone (a status from him is
 * refused with 403); moderation sends the status it moves the ticket to, and
 * may leave the text empty when it closes the ticket or marks it as spam.
 */
export type AnswerTicketRequest = {
  text: string;
  status?: AnswerStatus;
};

/**
 * The whole life of a ticket ("обращение"): filed by a visitor or a member,
 * talked over by its author and moderation, then closed by moderation with
 * its last answer.
 *
 * All three phases speak about the same entity over the same wire shape, so
 * they share one client — splitting by caller role is what left the intake and
 * the moderation halves in two files with duplicate types.
 */
export default new (class TicketApi {
  /**
   * Submit a ticket ("обращение") from the /support or /complaint form.
   * Anonymous submissions are allowed - guests without an account
   * must be able to reach support. Returns the guest tracking token when the
   * author is a guest.
   *
   * The flags are the caller's: the form prints the refusal and the wait in
   * itself and claims both at the press.
   */
  public createTicket(
    request: CreateTicketIntake,
    options?: { ownsRefusal?: boolean; ownsRateLimit?: boolean },
  ) {
    return Api.post<CreateTicketIntakeResult>("tickets", request, options);
  }

  /**
   * Get a page of tickets filed by the current user ("Мои обращения").
   * GET /v1/moderation/tickets/mine - authentication required. Status and
   * subtype are filtered server-side (omit for all), and so is the page: the
   * pile of a long-standing member only grows.
   *
   * The page number is the caller's own vocabulary; the wire takes skip/take
   * like every other list.
   */
  public getMyTickets(params?: {
    status?: TicketStatus;
    subtype?: TicketSubtype;
    /** 1-based page number */
    number?: number;
    /** Page size; clamped to what the wire accepts */
    take?: number;
  }) {
    const pageSize = Math.min(params?.take ?? DEFAULT_PAGE_SIZE, MAX_PAGE_SIZE);
    const pageNumber = params?.number ?? 1;
    return Api.get<ListEnvelope<Ticket>>("moderation/tickets/mine", {
      status: params?.status,
      subtype: params?.subtype,
      skip: pageNumber > 1 ? (pageNumber - 1) * pageSize : undefined,
      take: pageSize,
    });
  }

  /**
   * Track a guest ticket by its tracking token (public, no auth).
   * GET /v1/tickets/track, token in the X-Dm-Ticket-Token header — the token is
   * the only credential of this call, and a URL is written verbatim into the
   * proxy access log. Response is wrapped in Envelope.
   */
  public trackTicket(token: string) {
    return Api.get<Envelope<TrackedTicket>>(
      "tickets/track",
      undefined,
      undefined,
      { skipAuth: true, headers: { [X_DM_TICKET_TOKEN]: token } },
    );
  }

  /**
   * Write a comment in the conversation of a guest ticket, as its guest.
   * POST /v1/tickets/track/comments, the token in the X-Dm-Ticket-Token header
   * for the reason the read above carries it there. The comment is the guest's
   * because it came by the token: the server writes it with no author, whoever
   * may be signed in.
   */
  public createTrackedComment(token: string, text: string) {
    return Api.post<Envelope<TicketComment>>(
      "tickets/track/comments",
      { text },
      { headers: { [X_DM_TICKET_TOKEN]: token } },
    );
  }

  // ==================== Moderation view ====================

  /**
   * Get tickets visible to the caller role (Moderator+), optionally
   * filtered by status and subtype. GET v1/moderation/tickets.
   */
  public getTickets(params?: {
    status?: TicketStatus;
    subtype?: TicketSubtype;
  }) {
    return Api.get<ListEnvelope<Ticket>>("moderation/tickets", {
      status: params?.status,
      subtype: params?.subtype,
    });
  }

  /**
   * Get a single ticket with its conversation: a moderator gets a ticket
   * whose reason his role is shown and which does not name him, an author his
   * own ("Мои обращения" opens one by it), anybody else a 404.
   */
  public getTicket(ticketId: string) {
    return Api.get<Envelope<TicketDetails>>(`moderation/tickets/${ticketId}`);
  }

  /**
   * Answer in the conversation of a ticket, as its author or as a moderator
   * to whom its reason is shown. POST /v1/moderation/tickets/{id}/comments.
   * Who wrote it is the session's to say. The author's answer hands the
   * ticket to moderation; moderation's moves it to the status sent with it,
   * waiting for the author when none is. The ticket comes back whole, as it
   * stands after the answer: a closing changes what the page offers, and a
   * closing without a text writes no comment at all.
   */
  public createComment(ticketId: string, request: AnswerTicketRequest) {
    return Api.post<Envelope<TicketDetails>>(
      `moderation/tickets/${ticketId}/comments`,
      request,
    );
  }
})();
