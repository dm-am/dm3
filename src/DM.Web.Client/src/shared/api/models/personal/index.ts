/**
 * Personal API models
 */

// Re-export blacklist types
export * from "./blacklist";

/**
 * Theme preference
 */
export enum Theme {
  Light = "Light",
  Dark = "Dark",
}

/**
 * Paging preferences for different entity types
 */
export type Paging = {
  postsPerPage: number;
  commentsPerPage: number;
  topicsPerPage: number;
  messagesPerPage: number;
  entitiesPerPage: number;
};

/**
 * User display preferences (not part of profile)
 * Controls theme and pagination settings
 */
export type Preferences = {
  theme: Theme;
  paging: Paging;
  /**
   * IANA name of the zone the account keeps time in ("Europe/Moscow"). Null is
   * UTC.
   *
   * The letters read it, having no browser to ask, and so does the site: a
   * signed-in reader sees every time of the site in it (siteTime in
   * shared/lib/utils/datetime.ts), a guest in the zone of the device.
   */
  timeZoneId: string | null;
};

/**
 * The settings blob carried on the user DTO: the preferences above plus the
 * fields only the owner of the account sees.
 */
export type UserSettings = {
  mentorGreetingsMessage: string;
  theme: Theme;
  paging: Paging | null;
};

/**
 * Invitation received by the current user
 */
export type ReceivedInvitation = {
  id: string;
  entityId: string;
  entityType: "game" | "blog";
  entityTitle: string;
  inviterUsername: string;
  type: "assistant" | "player" | "reader";
  createdUtc: string;
};
