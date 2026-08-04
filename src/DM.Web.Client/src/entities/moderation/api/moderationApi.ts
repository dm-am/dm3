import type { ModeratedProfile } from "@/shared/api/models/moderation";
import type { Username } from "@/shared/api/models/community";
import type {
  ListEnvelope,
  Envelope,
  PagingQuery,
  User,
  UserRef,
} from "@/shared/api/models/common";
import type { Upload } from "@/shared/api/models/common/upload";
import { Api } from "@/shared/api";

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

/**
 * Everything a moderator does that is not tied to one content type: the team
 * roster, violators, warnings and bans, the premoderation queues, sitewide
 * uploads and username change requests.
 *
 * Two neighbours were split off because they belong to their own domain rather
 * than to this one: the game tag catalog (entities/game) and tickets
 * (entities/ticket).
 */
export default new (class ModerationApi {
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

  public removeWarning(warningId: string) {
    return Api.delete(`moderation/warnings/${warningId}`);
  }

  /**
   * Public ban status for a user (type + period only — no reason, no
   * moderator identity). Same trimmed shape the backend returns from
   * GET users/{username}/bans for every caller, moderator or not.
   */
  public getBans(username: Username) {
    return Api.get<PublicUserBanStatus>(`users/${username}/bans`);
  }

  public liftBan(banId: string) {
    return Api.delete(`bans/${banId}`);
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

export enum BanType {
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
