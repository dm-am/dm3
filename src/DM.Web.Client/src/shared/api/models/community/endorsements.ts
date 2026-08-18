import type { Id, Served } from "../common";
import type { User } from "./index";

/**
 * User endorsement types
 * @module shared/api/models/community/endorsements
 *
 * Endorsements are positive-only recommendations between users who have played together.
 * - Plain text (no BBCode)
 * - One endorsement per author-target pair
 * - NO likes support
 *
 * @see src/DM.Web.API/Features/Community/Endorsements/UserEndorsementDtos.cs
 */

export type UserEndorsementId = Id<string>;

/**
 * User endorsement DTO - positive recommendation about another user
 */
export type UserEndorsement = {
  id: Served<UserEndorsementId>;
  author?: Served<User>;
  targetUser?: Served<User>;
  /** Endorsement text content (plain text, no BBCode) */
  text: string;
  createdUtc: Served<string>;
  modifiedUtc?: Served<string>;
};

/**
 * The server's answer to "may I recommend this user?" — asked before the
 * control that would POST is drawn.
 *
 * It is produced by the same evaluation the POST refuses by (signed in, not
 * oneself, past probation, played in the same game, no recommendation for the
 * pair yet), so a client that draws the control only on `canCreate` never
 * offers what the create call would reject. `reason` is the server's own
 * sentence and is shown as-is.
 *
 * @see src/DM.Web.API/Features/Community/Endorsements/UserEndorsementDtos.cs
 */
export type EndorsementEligibility = {
  canCreate: boolean;
  /** Why not, in the server's words. Absent when `canCreate` is true. */
  reason?: string;
};

/**
 * Request to create a new user endorsement
 */
export type CreateUserEndorsementRequest = {
  text: string;
};

/**
 * Request to update an existing user endorsement
 */
export type UpdateUserEndorsementRequest = {
  text?: string;
};
