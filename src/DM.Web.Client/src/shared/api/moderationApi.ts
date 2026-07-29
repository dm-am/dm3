import type { ModeratedProfile } from "./models/moderation";
import type { Username } from "./models/community";
import type {
  ListEnvelope,
  Envelope,
  PagingQuery,
  User,
  UserRef,
} from "./models/common";
import type { Upload } from "./models/common/upload";
import type { TicketSubtype } from "./supportApi";
import Api from "./client";

// ==================== Tag Management Types ====================

export type ModerationTagGroup = {
  id: string;
  title: string;
  description?: string;
  sortOrder: number;
  tagsCount: number;
};

export type ModerationTag = {
  id: string;
  shortId: number;
  groupId: string;
  groupTitle: string;
  title: string;
  description?: string;
  sortOrder: number;
  gamesCount: number;
};

export type CreateTagGroupRequest = {
  title: string;
  description?: string;
  sortOrder: number;
};

export type UpdateTagGroupRequest = {
  title: string;
  description?: string;
  sortOrder: number;
};

export type CreateTagRequest = {
  groupId: string;
  title: string;
  description?: string;
  sortOrder: number;
};

export type UpdateTagRequest = {
  groupId: string;
  title: string;
  description?: string;
  sortOrder: number;
};

// ==================== Moderation Team Types ====================

/** A single zone of responsibility (forum board, game, or blog) */
export type ModerationZone = {
  id: string;
  title: string;
};

/**
 * Moderation team member with zones of responsibility
 * (ModeratorsController.cs GET v1/moderation/moderators).
 */
export type ModeratorOverview = {
  user: User;
  boards: ModerationZone[];
  curatedGames: ModerationZone[];
  curatedBlogs: ModerationZone[];
};

// ==================== Violator Types ====================

/**
 * Violators list row (ViolatorController.cs GET v1/moderation/violators):
 * a user with active warning points or an active ban.
 */
export type Violator = {
  user: UserRef;
  /** Current active warning points (the N in "N/6") */
  points: number;
  /** Auto-ban points threshold (the 6 in "N/6") */
  pointsThreshold: number;
  /** Moment of the latest active warning (null if the user only has a ban) */
  lastWarningUtc?: string | null;
  /** Active ban details (null if the user only has warning points) */
  activeBan?: Ban | null;
};

export type ViolatorsFilter = "all" | "banned" | "points-only";

// ==================== Premoderation Types ====================

/** Mirrors backend PremoderationStatus enum (ModuleStatus.cs) */
export type PremoderationStatus =
  | "Approved"
  | "AwaitingApproval"
  | "AwaitingEdits";

/** Mirrors backend Game/BlogPremoderationTransition */
export type PremoderationTransition =
  | "SendToPremoderation"
  | "RemoveFromPremoderation";

/**
 * Minimal game projection for the premoderation review queue.
 * Structural subset of the backend Game DTO (GameRef + createdUtc) —
 * only the fields the moderation list renders.
 */
export type PremoderatedGame = {
  id: string;
  publicId: string;
  title: string;
  master: UserRef;
  createdUtc: string;
};

/**
 * Minimal blog projection for the premoderation review queue.
 * Structural subset of the backend Blog DTO (BlogRef).
 */
export type PremoderatedBlog = {
  id: string;
  title: string;
  author: UserRef;
  createdUtc: string;
};

// ==================== Ticket Types ====================

/** Mirrors backend TicketStatus enum (Russian labels from Descriptions) */
export type TicketStatus =
  | "WaitingForModeration"
  | "WaitingForUser"
  | "Closed"
  | "Spam";

/**
 * Moderation ticket ("обращение") — TicketController.cs / TicketDtos.cs.
 * Visibility of subtypes is role-scoped server-side.
 */
export type Ticket = {
  id: string;
  /** Reporter username (null for guest submissions) */
  reporterUsername?: string | null;
  /** Target user username (null for tickets without a target) */
  targetUsername?: string | null;
  /** Contact email left by a guest author (null for authenticated authors) */
  guestEmail?: string | null;
  entityId?: string | null;
  entityType?: string | null;
  status: TicketStatus;
  subtype: TicketSubtype;
  createdUtc: string;
  /** Reported content snapshot / ticket subject+text */
  description: string;
  /** Reporter's comment */
  comment: string;
  assignedModeratorUsername?: string | null;
  resolvedUtc?: string | null;
  /** Moderator's response */
  answer?: string | null;
  hasWarning: boolean;
  hasBan: boolean;
};

/** Mirrors backend ResolveTicketRequest */
export type ResolveTicketRequest = {
  status: TicketStatus;
  answer: string;
  issueWarning?: boolean;
  warningText?: string;
  /** 0-6, 0 = verbal warning */
  warningPoints?: number;
  /** SeniorModerator+ only */
  issueBan?: boolean;
  banDurationHours?: number | null;
  banComment?: string;
};

/** Mirrors backend TicketStats (GET v1/moderation/tickets/stats) */
export type TicketStats = {
  waitingForModeration: number;
  waitingForUser: number;
  closed: number;
  spam: number;
};

export default new (class moderationApi {
  public getModeratedProfile(username: Username) {
    return Api.get<ModeratedProfile>(`moderation/users/${username}/profile`);
  }

  // ==================== Moderation Team ====================

  /**
   * Get the moderation team with zones of responsibility (Moderator+).
   * GET v1/moderation/moderators — highest role first, then by username.
   */
  public getModerators() {
    return Api.get<ListEnvelope<ModeratorOverview>>("moderation/moderators");
  }

  // ==================== Violators ====================

  /**
   * Get users with active warning points or an active ban (Moderator+),
   * sorted by points descending. GET v1/moderation/violators.
   */
  public getViolators(filter: ViolatorsFilter = "all") {
    return Api.get<ListEnvelope<Violator>>("moderation/violators", { filter });
  }

  // ==================== Ban History / All Warnings ====================

  /**
   * Get all bans ever issued — active, expired and lifted — newest
   * first, paged (Moderator+). GET v1/bans/history.
   */
  public getBanHistory(query?: PagingQuery) {
    return Api.get<ListEnvelope<Ban>>("bans/history", {
      skip: query?.skip,
      take: query?.take,
    });
  }

  /**
   * Get all warnings across the website (Moderator+), optionally
   * filtered by user login. GET v1/moderation/warnings.
   */
  public getAllWarnings(user?: string) {
    return Api.get<ListEnvelope<Warning>>(
      "moderation/warnings",
      user ? { user } : undefined,
    );
  }

  // ==================== Premoderation Queues ====================

  /**
   * Get games in the given premoderation status, oldest first
   * (GamesQuery.PremoderationStatuses — Mentor+ only, others get 403).
   */
  public getPremoderatedGames(
    status: Exclude<PremoderationStatus, "Approved">,
  ) {
    return Api.get<ListEnvelope<PremoderatedGame>>("games", {
      premoderationStatuses: [status],
      sortBy: "created",
      sortOrder: "asc",
      take: 100,
    });
  }

  /**
   * Get blogs in the given premoderation status, oldest first
   * (BlogsQuery.PremoderationStatus — honored for Mentor+ callers only).
   */
  public getPremoderatedBlogs(
    status: Exclude<PremoderationStatus, "Approved">,
  ) {
    return Api.get<ListEnvelope<PremoderatedBlog>>("blogs", {
      premoderationStatus: status,
      sortBy: "created",
      sortOrder: "asc",
      take: 100,
    });
  }

  /** Change game premoderation state (Mentor+). POST v1/games/{id}/premoderation */
  public changeGamePremoderation(
    id: string,
    transition: PremoderationTransition,
  ) {
    return Api.post<Envelope<unknown>>(`games/${id}/premoderation`, {
      transition,
    });
  }

  /** Change blog premoderation state (Mentor+). POST v1/blogs/{id}/premoderation */
  public changeBlogPremoderation(
    id: string,
    transition: PremoderationTransition,
  ) {
    return Api.post<Envelope<unknown>>(`blogs/${id}/premoderation`, {
      transition,
    });
  }

  // ==================== Tickets (role-scoped moderation view) ====================

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

  /** Get ticket counts grouped by status (Moderator+). */
  public getTicketStats() {
    return Api.get<TicketStats>("moderation/tickets/stats");
  }

  /** Get a single ticket (moderators see full details). */
  public getTicket(ticketId: string) {
    return Api.get<Envelope<Ticket>>(`moderation/tickets/${ticketId}`);
  }

  /** Assign a ticket to the current moderator. */
  public assignTicketToMe(ticketId: string) {
    return Api.post<Envelope<Ticket>>(`moderation/tickets/${ticketId}/assign`);
  }

  /**
   * Resolve a ticket with an answer, target status and optional
   * warning/ban (ban part requires SeniorModerator+ server-side).
   */
  public resolveTicket(ticketId: string, request: ResolveTicketRequest) {
    return Api.post<Envelope<Ticket>>(
      `moderation/tickets/${ticketId}/resolve`,
      request,
    );
  }

  // ==================== Uploads (admin scope) ====================

  /**
   * Get uploads across the website (Admin only server-side:
   * UploadIntention.ListAll/ListUser). Passing `username` narrows to one
   * user's uploads; otherwise scope=all returns everything. Newest first.
   */
  public getAllUploads(params?: {
    username?: string;
    /** 1-based page number (UploadsQuery.Number) */
    number?: number;
    /** page size 1-100 (UploadsQuery.Size) */
    size?: number;
  }) {
    const username = params?.username?.trim();
    return Api.get<ListEnvelope<Upload>>("uploads", {
      ...(username ? { username } : { scope: "all" }),
      number: params?.number,
      size: params?.size,
    });
  }

  /** Delete (soft-delete) an upload. Others' uploads require Admin. */
  public deleteUpload(id: string) {
    return Api.delete(`uploads/${id}`);
  }

  // ==================== Username Change Requests ====================

  /**
   * Get all pending username change requests
   */
  public getPendingUsernameChangeRequests() {
    return Api.get<ListEnvelope<UsernameChangeRequest>>(
      "moderation/username-changes",
    );
  }

  /**
   * Get a specific username change request
   */
  public getUsernameChangeRequest(id: string) {
    return Api.get<Envelope<UsernameChangeRequest>>(
      `moderation/username-changes/${id}`,
    );
  }

  /**
   * Resolve (approve/reject) a username change request
   */
  public resolveUsernameChangeRequest(
    id: string,
    resolve: ResolveUsernameChangeRequest,
  ) {
    return Api.patch<Envelope<UsernameChangeRequest>>(
      `moderation/username-changes/${id}`,
      resolve,
    );
  }

  public createModNote(username: Username, text: string) {
    return Api.post<ModeratedProfileNote>(
      `moderation/users/${username}/notes`,
      { text },
    );
  }

  public updateModNote(noteId: string, text: string) {
    return Api.put<ModeratedProfileNote>(`moderation/notes/${noteId}`, {
      text,
    });
  }

  public deleteModNote(noteId: string) {
    return Api.delete(`moderation/notes/${noteId}`);
  }

  /**
   * Public warnings summary for a user (points + dates only — no reason,
   * no moderator identity). Same trimmed shape the backend returns from
   * GET users/{username}/warnings for every caller, moderator or not.
   */
  public getWarnings(username: Username) {
    return Api.get<UserWarningsInfo>(`users/${username}/warnings`);
  }

  public createWarning(warning: CreateWarning) {
    return Api.post<Warning>("warnings", warning);
  }

  public removeWarning(warningId: string) {
    return Api.delete(`warnings/${warningId}`);
  }

  /**
   * Public ban status for a user (type + period only — no reason, no
   * moderator identity). Same trimmed shape the backend returns from
   * GET users/{username}/bans for every caller, moderator or not.
   */
  public getBans(username: Username) {
    return Api.get<PublicUserBanStatus>(`users/${username}/bans`);
  }

  public createBan(ban: CreateBan) {
    return Api.post<Ban>("bans", ban);
  }

  public liftBan(banId: string) {
    return Api.delete(`bans/${banId}`);
  }

  // ==================== Tag Management ====================

  /**
   * Get all tag groups
   */
  public getTagGroups() {
    return Api.get<ListEnvelope<ModerationTagGroup>>("moderation/tags/groups");
  }

  /**
   * Get a tag group by ID
   */
  public getTagGroup(groupId: string) {
    return Api.get<ModerationTagGroup>(`moderation/tags/groups/${groupId}`);
  }

  /**
   * Create a new tag group
   */
  public createTagGroup(request: CreateTagGroupRequest) {
    return Api.post<ModerationTagGroup>("moderation/tags/groups", request);
  }

  /**
   * Update a tag group
   */
  public updateTagGroup(groupId: string, request: UpdateTagGroupRequest) {
    return Api.put<ModerationTagGroup>(
      `moderation/tags/groups/${groupId}`,
      request,
    );
  }

  /**
   * Delete a tag group
   */
  public deleteTagGroup(groupId: string) {
    return Api.delete(`moderation/tags/groups/${groupId}`);
  }

  /**
   * Get all tags
   */
  public getTags() {
    return Api.get<ListEnvelope<ModerationTag>>("moderation/tags");
  }

  /**
   * Get tags by group ID
   */
  public getTagsByGroup(groupId: string) {
    return Api.get<ListEnvelope<ModerationTag>>(
      `moderation/tags/groups/${groupId}/tags`,
    );
  }

  /**
   * Get a tag by ID
   */
  public getTag(tagId: string) {
    return Api.get<ModerationTag>(`moderation/tags/${tagId}`);
  }

  /**
   * Create a new tag
   */
  public createTag(request: CreateTagRequest) {
    return Api.post<ModerationTag>("moderation/tags", request);
  }

  /**
   * Update a tag
   */
  public updateTag(tagId: string, request: UpdateTagRequest) {
    return Api.put<ModerationTag>(`moderation/tags/${tagId}`, request);
  }

  /**
   * Delete a tag
   */
  public deleteTag(tagId: string) {
    return Api.delete(`moderation/tags/${tagId}`);
  }
})();

export type ModeratedProfileNote = {
  id: string;
  text: string;
  author?: { id: string; username: string };
  createdUtc: string;
  modifiedUtc?: string;
};

/**
 * Warning with full moderation detail (reason, moderator identity).
 * Returned by the moderator-only create endpoint (POST warnings);
 * NOT what GET users/{username}/warnings returns — that is
 * {@link PublicWarning}.
 */
export type Warning = {
  id: string;
  user?: UserRef;
  moderator?: UserRef;
  entityId?: string;
  entityType?: string;
  points: number;
  reason: string;
  createdUtc: string;
  isActive: boolean;
};

export type CreateWarning = {
  username: string;
  points: number;
  reason: string;
  entityId?: string;
  entityType?: string;
};

/**
 * Ban with full moderation detail (comment, moderator identity).
 * Returned by the moderator-only create endpoint (POST bans); NOT what
 * GET users/{username}/bans returns — that is {@link PublicBan}.
 */
export type Ban = {
  id: string;
  user?: { username: string };
  moderator?: { username: string };
  type: BanType;
  startedUtc: string;
  expiresUtc?: string;
  comment: string;
  isActive: boolean;
  liftedUtc?: string;
};

export type CreateBan = {
  username: string;
  type?: BanType;
  expiresUtc?: string;
  durationHours?: number;
  comment: string;
};

export enum BanType {
  Auto = "Auto",
  Temporary = "Temporary",
  Permanent = "Permanent",
  Voluntary = "Voluntary",
}

/**
 * Warning as visible to everyone on the public profile: aggregate facts
 * only, no reason and no moderator identity (WarningController.cs
 * GetUserWarnings / PublicWarning DTO).
 */
export type PublicWarning = {
  points: number;
  createdUtc: string;
  isActive: boolean;
};

/**
 * User warnings summary (public view) — GET users/{username}/warnings.
 */
export type UserWarningsInfo = {
  username: string;
  totalPoints: number;
  activeCount: number;
  warnings: PublicWarning[];
};

/**
 * Ban as visible to everyone on the public profile: type and period
 * only, no comment and no moderator identity (BanController.cs
 * GetUserBans / PublicBan DTO).
 */
export type PublicBan = {
  type: BanType;
  startedUtc: string;
  expiresUtc?: string;
  isActive: boolean;
};

/**
 * User ban status (public view) — GET users/{username}/bans.
 */
export type PublicUserBanStatus = {
  username: string;
  isBanned: boolean;
  activeBan?: PublicBan;
  history: PublicBan[];
};

// ==================== Username Change Request Types ====================

export enum UsernameChangeRequestStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
  Completed = 3,
  Expired = 4,
}

export type UsernameChangeRequest = {
  id: string;
  userId: string;
  currentUsername: string;
  requestedUsername?: string | null;
  reason: string;
  status: UsernameChangeRequestStatus;
  createdUtc: string;
  approvalExpiresUtc?: string | null;
  resolvedUtc?: string | null;
  resolvedBy?: string | null;
  comment?: string | null;
};

export type ResolveUsernameChangeRequest = {
  status: UsernameChangeRequestStatus;
  comment?: string | null;
};
