import type { Envelope, ListEnvelope } from "./models/common";
import Api from "./client";

// ==================== Ticket Intake Types ====================

/** Ticket ("обращение") categories; mirrors backend TicketSubtype */
export type TicketSubtype =
  | "UserComplaint"
  | "ModeratorDecisionComplaint"
  | "SeniorModeratorDecisionComplaint"
  | "SiteImprovementSuggestion"
  | "Bug"
  | "AccessRecovery"
  | "RegistrationIssue";

export type CreateTicketIntake = {
  subtype: TicketSubtype;
  subject: string;
  text: string;
  contact?: string;
  violationUrl?: string;
  targetUsername?: string;
  /** Honeypot field for bot protection - must stay empty */
  website?: string;
};

/**
 * Response of the intake submission. Mirrors backend
 * CreateTicketIntakeResponse: a guest tracking token (null for authenticated
 * authors, who track their tickets from "Мои обращения").
 */
export type CreateTicketIntakeResult = {
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
 * Ticket ("обращение") as returned by the moderation ticket endpoints.
 * Mirrors backend DM.Web.API.Features.Moderation.Tickets.Ticket DTO.
 *
 * Field semantics for intake-created tickets (the /support and /complaint
 * forms): `comment` holds the short subject line, `description` holds the
 * ticket body text (see TicketService.CreateIntakeTicket).
 */
export type Ticket = {
  id: string;
  /** Reporter username (null for guest submissions) */
  reporterUsername?: string | null;
  /** Username the complaint is about (null when not applicable) */
  targetUsername?: string | null;
  /** Contact email left by a guest author */
  guestEmail?: string | null;
  entityId?: string | null;
  entityType?: string | null;
  status: TicketStatus;
  subtype: TicketSubtype;
  createdUtc: string;
  /** Ticket body text (intake message) */
  description: string;
  /** Short subject line (intake Subject is stored here) */
  comment: string;
  /** Moderator assigned to the ticket */
  assignedModeratorUsername?: string | null;
  resolvedUtc?: string | null;
  /** Moderation's answer (null until answered) */
  answer?: string | null;
  hasWarning: boolean;
  hasBan: boolean;
};

/**
 * Public, token-gated view of a guest ticket (GET /v1/tickets/track/{token}).
 * Mirrors backend TrackedTicket — moderation internals are intentionally
 * omitted.
 */
export type TrackedTicket = {
  status: TicketStatus;
  subtype: TicketSubtype;
  /** Short subject line */
  subject: string;
  /** Ticket body text */
  description: string;
  createdUtc: string;
  resolvedUtc?: string | null;
  /** Moderation's answer (null until answered) */
  answer?: string | null;
  responses: TrackedTicketResponse[];
};

/** A single response in the guest-facing ticket thread */
export type TrackedTicketResponse = {
  text: string;
  createdUtc: string;
  isFromModerator: boolean;
};

export default new (class SupportApi {
  /**
   * Submit a ticket ("обращение") from the /support or /complaint form.
   * Anonymous submissions are allowed - guests without an account
   * must be able to reach support. Returns the guest tracking token when the
   * author is a guest.
   */
  public createTicket(request: CreateTicketIntake) {
    return Api.post<CreateTicketIntakeResult>("tickets", request);
  }

  /**
   * Get tickets filed by the current user ("Мои обращения").
   * GET /v1/moderation/tickets/mine - authentication required. Status and
   * subtype are filtered server-side (omit for all).
   */
  public getMyTickets(params?: {
    status?: TicketStatus;
    subtype?: TicketSubtype;
  }) {
    return Api.get<ListEnvelope<Ticket>>("moderation/tickets/mine", {
      status: params?.status,
      subtype: params?.subtype,
    });
  }

  /**
   * Track a guest ticket by its tracking token (public, no auth).
   * GET /v1/tickets/track/{token}. Response is wrapped in Envelope.
   */
  public trackTicket(token: string) {
    return Api.get<Envelope<TrackedTicket>>(
      `tickets/track/${encodeURIComponent(token)}`,
      undefined,
      undefined,
      { skipAuth: true },
    );
  }
})();
