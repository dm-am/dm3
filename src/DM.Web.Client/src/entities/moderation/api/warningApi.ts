import type { Username, UserPicture } from "@/shared/api/models/community";
import type {
  Envelope,
  ListEnvelope,
  PagingQuery,
  UserRef,
} from "@/shared/api/models/common";
import { Api } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

export default new (class WarningApi {
  /** Open to everybody; the text and its place only where the reader reads the place. */
  public getWarningLog(query?: WarningsQuery) {
    return Api.get<ListEnvelope<WarningLogEntry>>("warnings", {
      username: query?.username,
      skip: query?.skip,
      take: query?.take,
    });
  }

  public getAllWarnings(query?: WarningsQuery) {
    return Api.get<ListEnvelope<Warning>>("moderation/warnings", {
      username: query?.username,
      skip: query?.skip,
      take: query?.take,
    });
  }

  /** The points and the dates only, for every caller, a moderator too. */
  public getWarnings(username: Username) {
    return Api.get<UserWarningsInfo>(
      `users/${encodeURIComponent(username)}/warnings`,
    );
  }

  // A warning is the record of a decision and is never withdrawn: only its points move.
  public changeWarningPoints(
    warningId: string,
    change: ChangeWarningPoints,
    options?: RequestOptions,
  ) {
    return Api.patch<Envelope<Warning>>(
      `moderation/warnings/${warningId}`,
      change,
      options,
    );
  }

  /** For good; a second press answers success too. */
  public removeWarningProfilePicture(
    warningId: string,
    options?: RequestOptions,
  ) {
    return Api.delete(
      `moderation/warnings/${warningId}/profile-picture`,
      undefined,
      options,
    );
  }
})();

type WarningsQuery = PagingQuery & {
  /** A name nobody carries is an empty page. */
  username?: string;
};

export type WarningPlaceKind =
  "Topic" | "Game" | "Blog" | "GlobalChat" | "Publication";

/** What the open log and the moderation list both carry. */
export type WarningRecord = {
  id: string;
  /** As they stand now. */
  points: number;
  /** As issued, before any correction. */
  originalPoints: number;
  /** Public with the warning: without it a reader sees a verdict that no longer stands. */
  pointsChangedBy?: string | null;
  pointsChangedUtc?: string | null;
  pointsChangeText?: string | null;
  /** Rendered. */
  reason: string;
  createdUtc: string;
  /**
   * The text as the server took it at issue, rendered: it outlives an edit and a deletion. In the
   * open log absent where the reader does not read the place or none is found.
   */
  evidence?: string | null;
  placeKind?: WarningPlaceKind | null;
  /** Absent for the global chat, and in the open log for a place the reader does not read. */
  placeTitle?: string | null;
  /** In the open log present only where the reader could follow it. */
  entityUrl?: string | null;
  entityCreatedUtc?: string | null;
  profile?: WarningProfile | null;
};

/** A row of the open log, by its reader: the text only where he reads the place. */
export type WarningLogEntry = WarningRecord & {
  user: UserRef;
  moderator: UserRef;
  /**
   * The reader does not read the place of the text, or none is found: the row
   * stands for it with one block of restricted access, nothing of the place.
   */
  accessRestricted?: boolean | null;
  /** "Profile" for a warning on the profile; absent for one that names nothing. */
  entityType?: string | null;
};

/** The profile as the server kept it at the moment of issue; a part it lacked is absent. */
type WarningProfile = {
  status?: string | null;
  /** Rendered as the profile renders it to a guest. */
  info?: string | null;
  /** Gone once moderation took it out. */
  picture?: UserPicture | null;
};

/** With the reason and the moderator; a profile's public view is {@link PublicWarning}. */
export type Warning = WarningRecord & {
  user?: UserRef;
  moderator?: UserRef;
  entityId?: string;
  entityType?: string;
  /** Decided by the server per reader and per row. */
  canChangePoints: boolean;
};

/** Points 0-6, the explanation required. */
type ChangeWarningPoints = {
  points: number;
  reason: string;
};

type PublicWarning = {
  /** A warning corrected to zero keeps its place and stops weighing on the total. */
  points: number;
  createdUtc: string;
};

export type UserWarningsInfo = {
  username: string;
  totalPoints: number;
  activeCount: number;
  warnings: PublicWarning[];
};
