import type { InvitationType } from "../game/invitations";

export * from "./blacklist";

export enum Theme {
  Light = "Light",
  Dark = "Dark",
}

/** Display preferences, not part of the profile. */
export type Preferences = {
  /** The theme a device takes the first time this account signs in on it. */
  theme: Theme;
  /** How many rows, posts, comments or messages a page of every paged list holds. */
  pageSize: number;
  /**
   * IANA zone of the account ("Europe/Moscow"); absent is UTC, since the API
   * leaves a null out of the answer. The letters read it, and a signed-in
   * reader sees every time of the site in it (siteTime).
   */
  timeZoneId?: string;
};

/**
 * What the viewer carries of the preferences, put there by the session, since
 * the user DTO carries no settings: the theme a new device starts from and the
 * page size the lists read.
 */
export type UserSettings = {
  theme?: Theme;
  pageSize: number | null;
};

export type ReceivedInvitation = {
  id: string;
  entityId: string;
  entityType: "game" | "blog";
  entityTitle: string;
  inviterUsername: string;
  type: InvitationType;
  createdUtc: string;
};
