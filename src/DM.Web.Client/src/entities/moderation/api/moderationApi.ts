import type { ModeratedProfile } from "@/shared/api/models/moderation";
import type { Username } from "@/shared/api/models/community";
import type {
  ListEnvelope,
  Envelope,
  PagingQuery,
  User,
  UserRef,
  UserRole,
  PremoderationStatus,
} from "@/shared/api/models/common";
import type { Upload } from "@/shared/api/models/common/upload";
import { Api } from "@/shared/api";
import { pageSkip } from "@/shared/api/pageSkip";
import type { Ban } from "./banApi";
import type { RequestOptions } from "@/shared/api/interceptor";

/** A forum board, a game or a blog. */
type ModerationZone = {
  id: string;
  title: string;
};

export type ModeratorOverview = {
  user: User;
  boards: ModerationZone[];
  curatedGames: ModerationZone[];
  curatedBlogs: ModerationZone[];
};

/** A user with active warning points or an active ban. */
export type Violator = {
  user: UserRef;
  /** The N in "N/6". */
  points: number;
  /** The 6 in "N/6": the total the site bans at by itself (WarningPolicy.AutoBanPointsThreshold). */
  pointsThreshold: number;
  lastWarningUtc?: string | null;
  activeBan?: Ban | null;
};

export type ViolatorsFilter = "all" | "banned" | "points-only";

/** Only the fields the queue draws. */
export type PremoderatedGame = {
  id: string;
  publicId: string;
  title: string;
  master: UserRef;
  createdUtc: string;
};

/** Only the fields the queue draws. */
export type PremoderatedBlog = {
  id: string;
  title: string;
  author: UserRef;
  createdUtc: string;
};

// What a moderator does that is not tied to one content type.
export default new (class ModerationApi {
  public getModeratedProfile(username: Username) {
    return Api.get<ModeratedProfile>(`moderation/users/${username}/profile`);
  }

  /** Answers with the rebuilt profile, so the caller needs no second read. */
  public setModerationWatch(username: Username, underWatch: boolean) {
    return Api.patch<Envelope<ModeratedProfile>>(
      `moderation/users/${username}/moderation-watch`,
      { underWatch },
    );
  }

  /** Highest role first, then by name. */
  public getModerators() {
    return Api.get<ListEnvelope<ModeratorOverview>>("moderation/moderators");
  }

  public getViolators(banState: ViolatorsFilter = "all") {
    return Api.get<ListEnvelope<Violator>>("moderation/violators", {
      banState,
    });
  }

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

  /** Without `username`, the uploads of everybody. */
  public getAllUploads(params?: {
    username?: string;
    /** 1-based. */
    number?: number;
    size?: number;
  }) {
    const username = params?.username?.trim();
    const pageSize = params?.size ?? 20;
    const pageNumber = params?.number ?? 1;
    return Api.get<ListEnvelope<Upload>>("uploads", {
      ...(username ? { username } : { allUsers: true }),
      skip: pageSkip(pageNumber, pageSize),
      take: pageSize,
    });
  }

  public getPendingUsernameChangeRequests() {
    return Api.get<ListEnvelope<UsernameChangeRequest>>(
      "moderation/username-changes",
    );
  }

  public resolveUsernameChangeRequest(
    id: string,
    resolve: ResolveUsernameChangeRequest,
    options?: RequestOptions,
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

  public deleteModNote(noteId: string, options?: RequestOptions) {
    return Api.delete(`moderation/notes/${noteId}`, undefined, options);
  }

  // The reason goes in the body: addresses end up in logs and referrers.
  public removeAccount(
    username: string,
    reason: string,
    options?: RequestOptions,
  ) {
    return Api.delete(
      `moderation/users/${encodeURIComponent(username)}/account`,
      {
        reason,
      },
      options,
    );
  }

  public restoreAccount(username: string, options?: RequestOptions) {
    return Api.post(
      `moderation/users/${encodeURIComponent(username)}/account`,
      undefined,
      options,
    );
  }

  /** The one list that sees a removed account; `search` matches a name or an address. */
  public getRemovedAccounts(query?: RemovedAccountsQuery) {
    return Api.get<ListEnvelope<RemovedAccount>>("moderation/users/removed", {
      search: query?.search,
      skip: query?.skip,
      take: query?.take,
    });
  }
})();

type ModeratedProfileNote = {
  id: string;
  text: string;
  author?: { id: string; username: string };
  createdUtc: string;
  modifiedUtc?: string;
};

/** By name, as every enum of the API is written. */
export enum UsernameChangeRequestStatus {
  Pending = "Pending",
  Approved = "Approved",
  Rejected = "Rejected",
  /** @public Only the server writes it. */ Completed = "Completed",
  /** @public Only the server writes it. */ Expired = "Expired",
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

/** All a moderator holding a support letter has to recognise the account by. */
export type RemovedAccount = {
  id: string;
  username: string;
  /** Withheld by the server below an administrator, not hidden here. */
  email?: string | null;
  role: UserRole;
  registeredUtc: string;
  lastActivityUtc?: string | null;
};

type RemovedAccountsQuery = PagingQuery & {
  search?: string;
};
