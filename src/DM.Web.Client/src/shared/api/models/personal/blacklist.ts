/** @see src/DM.Web.API/Features/Personal/Blacklists/BlacklistEntry.cs */

export type BlacklistEntry = {
  id: string;
  username: string;
  createdUtc: string;
};

export type BlockUserRequest = {
  username: string;
};

export type BlacklistSettings = {
  hideComments: boolean;
  hideMessages: boolean;
  hideGames: boolean;
  hideBlogs: boolean;
  blockDirectMessages: boolean;
  autoPopulateContentBlacklist: boolean;
};
