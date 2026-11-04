import type { Envelope, ListEnvelope, User } from "@/shared/api/models/common";
import { Api, X_DM_TICKET_TOKEN } from "@/shared/api";
import { pageSkip } from "@/shared/api/pageSkip";
import { MAX_API_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import type { RequestOptions } from "@/shared/api/interceptor";

// PagingPolicy.DefaultPageSize, compared by PagingContractShould.
const DEFAULT_PAGE_SIZE = 20;

export type TicketSubtype =
  | "UserComplaint"
  | "ModeratorDecisionComplaint"
  | "SeniorModeratorDecisionComplaint"
  | "Bug"
  | "AccessRecovery"
  | "RegistrationIssue"
  | "AccountRemoval"
  | "AccountRestoration"
  | "PersonalData"
  | "AgreementQuestion";

type CreateTicketIntake = {
  subtype: TicketSubtype;
  /** The forms do not ask for one: a ticket without it is named by its reason. */
  subject?: string;
  /** In the site markup. */
  text: string;
  contact?: string;
  violationUrl?: string;
  targetUsername?: string;
  /** A honeypot: must stay empty. */
  website?: string;
};

/** The tracking token is a guest's only way back to the answer. */
type CreateTicketIntakeResult = {
  ticketId: string;
  trackingToken?: string | null;
};

export type TicketStatus =
  | "WaitingForModeration"
  | "WaitingForUser"
  | "Closed"
  | "Spam";

/** One wire shape whatever the role; the server decides which fields are filled. */
export type Ticket = {
  id: string;
  /** Null for a guest and for a removed account; `isGuest` tells the two apart. */
  reporter?: User | null;
  isGuest: boolean;
  /** For moderation, not a part of the text. */
  guestEmail?: string | null;
  entityId?: string | null;
  entityType?: string | null;
  status: TicketStatus;
  subtype: TicketSubtype;
  createdUtc: string;
  /** Rendered, the other fields of the form among its lines. */
  description: string;
  /** The subject line. */
  comment: string;
  commentsCount: number;
  resolvedUtc?: string | null;
};

/** Not edited, not deleted, not liked. */
export type TicketComment = {
  id: string;
  /** Null for the guest who filed it and for a removed account. */
  author?: User | null;
  isGuest: boolean;
  createdUtc: string;
  /** Rendered. */
  text: string;
};

export type TicketDetails = Ticket & {
  /** Oldest first. */
  comments: TicketComment[];
  canComment: boolean;
  /** Answers as moderation, choosing the status the ticket moves to. */
  canResolve: boolean;
  resolvedByUsername?: string | null;
};

/** A guest ticket read by its token, without the moderation internals. */
export type TrackedTicket = {
  status: TicketStatus;
  subtype: TicketSubtype;
  subject: string;
  /** Rendered. */
  description: string;
  createdUtc: string;
  resolvedUtc?: string | null;
  resolvedByUsername?: string | null;
  /** Oldest first. */
  comments: TicketComment[];
  canComment: boolean;
};

/** Waiting for moderation comes of the author's answer alone. */
export type AnswerStatus = Extract<
  TicketStatus,
  "WaitingForUser" | "Closed" | "Spam"
>;

/** The author sends the text alone; moderation sends a status, with or without text. */
type AnswerTicketRequest = {
  text: string;
  status?: AnswerStatus;
};

// One client for the whole life of a ticket, filed, talked over and closed.
export default new (class TicketApi {
  /** A guest may file one too: guests without an account must reach support. */
  public createTicket(request: CreateTicketIntake, options?: RequestOptions) {
    return Api.post<CreateTicketIntakeResult>("tickets", request, options);
  }

  public getMyTickets(params?: {
    status?: TicketStatus;
    subtype?: TicketSubtype;
    /** 1-based. */
    number?: number;
    /** Clamped to what the wire accepts. */
    take?: number;
  }) {
    const pageSize = Math.min(
      params?.take ?? DEFAULT_PAGE_SIZE,
      MAX_API_PAGE_SIZE,
    );
    const pageNumber = params?.number ?? 1;
    return Api.get<ListEnvelope<Ticket>>("moderation/tickets/mine", {
      status: params?.status,
      subtype: params?.subtype,
      skip: pageSkip(pageNumber, pageSize),
      take: pageSize,
    });
  }

  // The token goes in a header: an address is written verbatim into the proxy log.
  public trackTicket(token: string) {
    return Api.get<Envelope<TrackedTicket>>(
      "tickets/track",
      undefined,
      undefined,
      { skipAuth: true, headers: { [X_DM_TICKET_TOKEN]: token } },
    );
  }

  /** The guest's because it came by the token, whoever may be signed in. */
  public createTrackedComment(token: string, text: string) {
    return Api.post<Envelope<TicketComment>>(
      "tickets/track/comments",
      { text },
      { headers: { [X_DM_TICKET_TOKEN]: token } },
    );
  }

  public getTickets(params?: {
    status?: TicketStatus;
    subtype?: TicketSubtype;
  }) {
    return Api.get<ListEnvelope<Ticket>>("moderation/tickets", {
      status: params?.status,
      subtype: params?.subtype,
    });
  }

  public getTicket(ticketId: string) {
    return Api.get<Envelope<TicketDetails>>(`moderation/tickets/${ticketId}`);
  }

  /** The ticket comes back whole: a closing changes what the page offers. */
  public createComment(ticketId: string, request: AnswerTicketRequest) {
    return Api.post<Envelope<TicketDetails>>(
      `moderation/tickets/${ticketId}/comments`,
      request,
    );
  }
})();
