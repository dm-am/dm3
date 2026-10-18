import type { ModeratedProfile } from "@/shared/api/models/moderation";
import type { Username } from "@/shared/api/models/community";
import type {
  ListEnvelope,
  Envelope,
  PagingQuery,
  User,
  UserRef,
  UserRole,
} from "@/shared/api/models/common";
import type { Upload } from "@/shared/api/models/common/upload";
import type { UserPicture } from "@/shared/api/models/community";
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
  /** The 6 in "N/6": the scale the table draws points on, not a limit that
   * triggers anything. There is no automatic ban in DM3. */
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
 * The refusal a caller takes over because its own screen carries it: a window
 * prints a refused request in itself, and the response interceptor has to stay
 * quiet or the same sentence stands in two places. Every call that leaves it
 * out is unchanged.
 *
 * Declared here rather than imported: `shared/api` keeps its own copy of the
 * type to itself, and the client reads the two fields off the request by name,
 * so a structural twin carries them just as well.
 */
type RefusalFlags = {
  /** The screen shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The screen shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
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

  /**
   * Switch the moderation watch on a user, or clear it (Moderator+).
   * PATCH v1/moderation/users/{username}/moderation-watch — answers with the
   * rebuilt moderated profile in an envelope, so the caller needs no second
   * read. The bare profile of getModeratedProfile above is the older shape and
   * stays that way; a new endpoint does not join the legacy list.
   */
  public setModerationWatch(username: Username, underWatch: boolean) {
    return Api.patch<Envelope<ModeratedProfile>>(
      `moderation/users/${username}/moderation-watch`,
      { underWatch },
    );
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
  public getViolators(banState: ViolatorsFilter = "all") {
    return Api.get<ListEnvelope<Violator>>("moderation/violators", {
      banState,
    });
  }

  // ==================== Ban History / All Warnings ====================

  /**
   * The ban list (Moderator+), GET v1/bans/history: every issue and every
   * lift of a ban an entry of its own, newest first, a page at a time,
   * optionally narrowed to one user. The page counts entries, not bans; the
   * page of one entry is asked for by its number.
   */
  public getBanHistory(query?: BansQuery) {
    return Api.get<ListEnvelope<BanLogEntry>>("bans/history", {
      username: query?.username,
      entry: query?.entry,
      skip: query?.skip,
      take: query?.take,
    });
  }

  /**
   * Get the open warning log: every warning ever issued, newest first, paged,
   * optionally narrowed to one user. GET v1/warnings, no role required.
   *
   * A row carries who was warned, by whom, for how many points, why and when,
   * and the offending text with its place, always; the address of the text only
   * where a guest could follow it. The server decides which.
   */
  public getWarningLog(query?: WarningsQuery) {
    return Api.get<ListEnvelope<WarningLogEntry>>("warnings", {
      username: query?.username,
      skip: query?.skip,
      take: query?.take,
    });
  }

  /**
   * Get warnings across the website with the evidence attached (Moderator+),
   * a page at a time, optionally filtered by username.
   * GET v1/moderation/warnings.
   */
  public getAllWarnings(query?: WarningsQuery) {
    return Api.get<ListEnvelope<Warning>>("moderation/warnings", {
      username: query?.username,
      skip: query?.skip,
      take: query?.take,
    });
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
   * (BlogsQuery.PremoderationStatuses — honored for Mentor+ callers only).
   */
  public getPremoderatedBlogs(
    status: Exclude<PremoderationStatus, "Approved">,
  ) {
    return Api.get<ListEnvelope<PremoderatedBlog>>("blogs", {
      premoderationStatuses: [status],
      sortBy: "created",
      sortOrder: "asc",
      take: 100,
    });
  }

  // ==================== Uploads (admin scope) ====================

  /**
   * Get uploads across the website (Admin only server-side:
   * UploadIntention.ListAll/ListUser). Passing `username` narrows to one
   * user's uploads; otherwise allUsers=true returns everything. Newest first.
   */
  public getAllUploads(params?: {
    username?: string;
    /** 1-based page number; the wire takes skip/take like every other list. */
    number?: number;
    /** page size 1-100 */
    size?: number;
  }) {
    const username = params?.username?.trim();
    const pageSize = params?.size ?? 20;
    const pageNumber = params?.number ?? 1;
    return Api.get<ListEnvelope<Upload>>("uploads", {
      ...(username ? { username } : { allUsers: true }),
      skip: pageNumber > 1 ? (pageNumber - 1) * pageSize : undefined,
      take: pageSize,
    });
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
    options?: RefusalFlags,
  ) {
    return Api.patch<Envelope<UsernameChangeRequest>>(
      `moderation/username-changes/${id}`,
      resolve,
      options,
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

  public deleteModNote(noteId: string, options?: RefusalFlags) {
    return Api.delete(`moderation/notes/${noteId}`, undefined, options);
  }

  /**
   * Public warnings summary for a user (points + dates only — no reason,
   * no moderator identity). Same trimmed shape the backend returns from
   * GET users/{username}/warnings for every caller, moderator or not.
   */
  public getWarnings(username: Username) {
    return Api.get<UserWarningsInfo>(`users/${username}/warnings`);
  }

  /**
   * Change the points of a warning (Moderator+ on his own, SeniorModerator+ on
   * anybody's). PATCH v1/moderation/warnings/{id}.
   *
   * The warning itself stays: it is the record of a decision and there is no
   * way to withdraw it, so there is no delete here to go with this. What moves
   * is the number of points and the second signature under the warning, which
   * the answer carries back on the updated resource.
   */
  public changeWarningPoints(
    warningId: string,
    change: ChangeWarningPoints,
    options?: RefusalFlags,
  ) {
    return Api.patch<Envelope<Warning>>(
      `moderation/warnings/${warningId}`,
      change,
      options,
    );
  }

  /**
   * Takes the picture out of a warning on a profile (Moderator+).
   * DELETE v1/moderation/warnings/{id}/profile-picture.
   *
   * The avatar the profile had when the warning was issued stays in both logs
   * after the person changes it; a picture that may not be shown even as
   * evidence is taken out here, for good. The server answers success again
   * when the picture is already gone, so two moderators pressing at once both
   * read the record without it.
   */
  public removeWarningProfilePicture(
    warningId: string,
    options?: RefusalFlags,
  ) {
    return Api.delete(
      `moderation/warnings/${warningId}/profile-picture`,
      undefined,
      options,
    );
  }

  /**
   * Public ban status for a user (type + period only — no reason, no
   * moderator identity). Same trimmed shape the backend returns from
   * GET users/{username}/bans for every caller, moderator or not.
   */
  public getBans(username: Username) {
    return Api.get<PublicUserBanStatus>(`users/${username}/bans`);
  }

  /**
   * Lift a ban early (SeniorModerator+). DELETE v1/bans/{id} with the reason in
   * the body, the way the removal of an account sends its own: the reason is
   * required, because the ban list writes the lift as an entry of its own,
   * "Примечание от модератора:" and the reason under it.
   */
  public liftBan(banId: string, reason: string, options?: RefusalFlags) {
    return Api.delete(`bans/${banId}`, { reason }, options);
  }

  /**
   * Removes somebody's account, as a senior moderator, with the reason written
   * down. The reason travels in the body and not in the address: it is a record
   * about a person, and addresses end up in logs and referrers.
   */
  public removeAccount(
    username: string,
    reason: string,
    options?: RefusalFlags,
  ) {
    return Api.delete(
      `moderation/users/${encodeURIComponent(username)}/account`,
      {
        reason,
      },
      options,
    );
  }

  /**
   * Brings a removed account back, as a senior moderator. POST on the address
   * the removal DELETEs, and no body: the removal records why an account was
   * taken, and giving it back carries nothing of the kind.
   *
   * The whole of what restoration is here — a reader writes to support, a
   * person reads the letter and presses this. There is no request to file and
   * nothing to wait for.
   */
  public restoreAccount(username: string, options?: RefusalFlags) {
    return Api.post(
      `moderation/users/${encodeURIComponent(username)}/account`,
      undefined,
      options,
    );
  }

  /**
   * The removed accounts, a page at a time (SeniorModerator).
   * GET v1/moderation/users/removed.
   *
   * The one list that can see a removed account at all: such a row is hidden
   * from every other list, profile and lookup in the product. `search` matches
   * a name or an address, and a term nothing carries is an empty page rather
   * than a refusal. The address comes back to an administrator only, and the
   * server is the one that withholds it.
   */
  public getRemovedAccounts(query?: RemovedAccountsQuery) {
    return Api.get<ListEnvelope<RemovedAccount>>("moderation/users/removed", {
      search: query?.search,
      skip: query?.skip,
      take: query?.take,
    });
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
 * Filter and page for both warning lists: the open log and the moderation one.
 *
 * One shape for the two, because they narrow and page the same list and differ
 * only in who may ask and in what a row carries.
 */
export type WarningsQuery = PagingQuery & {
  /** Narrows the log to one user; a name nobody carries is an empty page. */
  username?: string;
};

/** Where the offending text stood, as the caption of a warning names it. */
export type WarningPlaceKind =
  | "Topic"
  | "Game"
  | "Blog"
  | "GlobalChat"
  | "Publication";

/**
 * A row of the open warning log, readable without an account.
 *
 * It carries the offending text always (the owner, 05.10: "Всегда
 * показывай"), rendered by the rules of its place, a hidden block erased for
 * everybody, with the place and the date of the text. The address of the text
 * comes only where a guest could follow it: not when the place is closed to
 * him or gone, nor when the text was taken off the site.
 */
export type WarningLogEntry = {
  id: string;
  user: UserRef;
  moderator: UserRef;
  /** Points as they stand now: what the user's total counts. */
  points: number;
  /** Points the warning was issued with, unchanged by a later correction. */
  originalPoints: number;
  /**
   * Name of the moderator who last changed the points, null while nobody has.
   *
   * The correction is public along with the warning: one moderator warned and
   * wrote why, another took the points off because it was no violation, and a
   * reader shown only the first of the two reads a verdict that no longer
   * stands. {@link pointsChangedUtc} is what says a correction happened.
   */
  pointsChangedBy?: string | null;
  pointsChangedUtc?: string | null;
  /** Why the points were changed, in the words of whoever changed them. */
  pointsChangeText?: string | null;
  /** Why, rendered: the warning window takes it in the BBCode editor. */
  reason: string;
  createdUtc: string;
  /**
   * Kind of the offending content ("Comment", "Topic", "Message"), "Profile"
   * for a warning on the profile of the warned user, absent for a warning that
   * names nothing (one issued on a ticket about a person).
   */
  entityType?: string | null;
  /** The offending text as it stood when the warning was issued, rendered HTML. */
  evidence?: string | null;
  placeKind?: WarningPlaceKind | null;
  /** The topic, the game, the blog or the publication; absent for the global chat. */
  placeTitle?: string | null;
  /** Absent where a guest could not follow it: the text or its place is gone or closed. */
  entityUrl?: string | null;
  /** When the offending text was written. */
  entityCreatedUtc?: string | null;
  /** The profile as it stood at the moment of issue, for a warning on it. */
  profile?: WarningProfile | null;
};

/**
 * What a profile showed everybody when a warning was issued on it, kept by the
 * server so the log shows what the warning was given for after the person
 * changes the profile. Null for every other kind of warning and for a removed
 * account; a part the profile did not have is absent.
 */
export type WarningProfile = {
  status?: string | null;
  /** "О себе", rendered as the profile renders it to a guest. */
  info?: string | null;
  /** The avatar of the moment; gone once moderation took it out. */
  picture?: UserPicture | null;
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
  /**
   * The offending text as it stood at the moment the warning was issued,
   * rendered HTML. Taken by the server, never rewritten: it outlives both an
   * edit and a deletion of the content it was taken from.
   */
  evidence?: string | null;
  /** Site-relative address of the offending content, when it has one. */
  entityUrl?: string | null;
  placeKind?: WarningPlaceKind | null;
  placeTitle?: string | null;
  entityCreatedUtc?: string | null;
  /** Points as they stand now: what the user's total counts. */
  points: number;
  /** Points the warning was issued with, unchanged by a later correction. */
  originalPoints: number;
  /** The second signature, as on {@link WarningLogEntry} and shown the same way. */
  pointsChangedBy?: string | null;
  pointsChangedUtc?: string | null;
  pointsChangeText?: string | null;
  /**
   * Whether this reader may change the points of this warning. Decided by the
   * server per reader and per row — a senior moderator may on any warning, a
   * moderator only on the ones he issued himself — so the list never has to
   * work the rule out from a role it holds.
   */
  canChangePoints: boolean;
  /** Why, rendered: the warning window takes it in the BBCode editor. */
  reason: string;
  createdUtc: string;
  /** The profile as it stood at the moment of issue, for a warning on it. */
  profile?: WarningProfile | null;
};

/** PATCH v1/moderation/warnings/{id} body: points 0-6, explanation required. */
export type ChangeWarningPoints = {
  points: number;
  reason: string;
};

/** What a ban forbids: the ordinary ("демократический") one, or everything. */
export type BanAccessPolicy = "DemocraticBan" | "FullBan";

/**
 * Ban with full moderation detail (comment, moderator identity), as the ban
 * list, the moderation answers and POST bans return it; NOT what
 * GET users/{username}/bans returns — that is {@link PublicBan}.
 */
export type Ban = {
  id: string;
  /** The banned user; absent for an account that is gone. */
  user?: UserRef | null;
  /** Who issued it; absent for an account that is gone. */
  moderator?: UserRef | null;
  type: BanType;
  /** Sent so the list names the scope in words instead of guessing it. */
  accessPolicy: BanAccessPolicy;
  startedUtc: string;
  expiresUtc?: string | null;
  /** Why, rendered: the ban window takes it in the BBCode editor. */
  comment: string;
  isActive: boolean;
  /**
   * Whether this reader may lift the ban now: answered by the rule that
   * refuses the lift itself, so a list that offers the lift by it never
   * offers one the server turns down.
   */
  canLift: boolean;
  liftedUtc?: string | null;
  /** Who lifted the ban early; absent while it runs, or for an account that is gone. */
  liftedBy?: UserRef | null;
  /** Why the ban was lifted early, as typed: a plain field. */
  liftReason?: string | null;
};

/**
 * One entry of the ban list: the issue of a ban at its start, or its lift at
 * the moment of the lift. A lifted ban gives two entries carrying the same
 * ban, the way DM2's bot wrote its ban log, a comment for each.
 */
export type BanLogEntry = {
  /**
   * The number of the entry in the whole list, counted from the oldest: it
   * never changes, and the list of one person shows the same numbers.
   */
  number: number;
  kind: "Issued" | "Lifted";
  occurredUtc: string;
  /** The ban as it stands now; its end is the end it had, lifted or not. */
  ban: Ban;
};

/** Filter and page of the ban list. */
export type BansQuery = PagingQuery & {
  /** Narrows the list to one user; a name nobody carries is an empty page. */
  username?: string;
  /**
   * The number of an entry whose page is asked for instead of the one `skip`
   * names; the answer's `paging.skip` tells which page it is. An entry the
   * list does not have leaves the page to `skip`.
   */
  entry?: number;
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
  /**
   * Points as they stand now. A warning corrected to zero keeps its place in
   * the list and stops weighing on the total: it is a record of a decision, and
   * nothing takes it off the profile.
   */
  points: number;
  createdUtc: string;
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

/**
 * The status as the server writes it: by name, as every enum of the API is
 * (JsonConfiguration, JsonStringEnumConverter). Numbered, the queue compared
 * the server's "Pending" with 0, matched nothing and stood empty with requests
 * waiting in it. The server reads the name back when a request is resolved.
 */
export enum UsernameChangeRequestStatus {
  Pending = "Pending",
  Approved = "Approved",
  Rejected = "Rejected",
  Completed = "Completed",
  Expired = "Expired",
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

// ==================== Removed Account Types ====================

/**
 * A removed account, as the only list that can still see one draws it.
 *
 * Such a row is hidden from every other list, profile and lookup in the
 * product, so the fields here are all a moderator holding a support letter has
 * to recognise the account it is about.
 */
export type RemovedAccount = {
  id: string;
  username: string;
  /** Withheld by the server below an administrator, not hidden here. */
  email?: string | null;
  role: UserRole;
  registeredUtc: string;
  lastActivityUtc?: string | null;
};

/**
 * Which removed accounts to list.
 *
 * One term for the two columns it matches: whoever wrote the support letter is
 * the one who decided whether he quoted a name or an address.
 */
export type RemovedAccountsQuery = PagingQuery & {
  search?: string;
};
