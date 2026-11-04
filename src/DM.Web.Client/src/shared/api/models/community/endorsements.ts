import type { Served } from "../common";
import type { User } from "./index";

/**
 * Positive-only recommendations between users who played together: plain
 * text, one per author and target, no likes.
 * @see src/DM.Web.API/Features/Community/Endorsements/UserEndorsementDtos.cs
 */

type UserEndorsementId = Served<string>;

export type UserEndorsement = {
  id: Served<UserEndorsementId>;
  author?: Served<User>;
  targetUser?: Served<User>;
  /** Plain text, no BBCode. */
  text: string;
  createdUtc: Served<string>;
  modifiedUtc?: Served<string>;
};

/**
 * "May I recommend this user?", asked before the control is drawn and answered
 * by the same evaluation the POST refuses by; reason is shown as-is.
 *
 * @see src/DM.Web.API/Features/Community/Endorsements/UserEndorsementDtos.cs
 */
export type EndorsementEligibility = {
  canCreate: boolean;
  /** Why not, in the server's words. Absent when `canCreate` is true. */
  reason?: string;
};

export type CreateUserEndorsementRequest = {
  text: string;
};
