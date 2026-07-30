/**
 * Personal blacklist API models
 * @see src/DM.Web.API/Features/Personal/Blacklists/BlacklistEntry.cs
 */

/**
 * Entry in user's blacklist
 */
export type BlacklistEntry = {
  id: string;
  username: string;
  createdUtc: string;
};

/**
 * Request to block a user
 */
export type BlockUserRequest = {
  username: string;
};

/**
 * Settings for blacklist behavior
 */
export type BlacklistSettings = {
  hideComments: boolean;
  hideMessages: boolean;
  hideGames: boolean;
  hideBlogs: boolean;
  blockDirectMessages: boolean;
  autoPopulateContentBlacklist: boolean;
};
