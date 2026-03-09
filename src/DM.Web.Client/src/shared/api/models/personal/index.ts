/**
 * Personal API models
 * @see src/DM.Web.API/Dto/Personal/
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
