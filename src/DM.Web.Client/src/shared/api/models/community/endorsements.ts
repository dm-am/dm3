import type { Id, Served } from "../common";
import type { User } from "./index";

/**
 * User endorsement types
 * @module shared/api/models/community/endorsements
 *
 * Endorsements are positive-only recommendations between users who have played together.
 * - BBCode supported in text
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
  /** Endorsement text content (BBCode supported) */
  text: string;
  createdUtc: Served<string>;
  modifiedUtc?: Served<string>;
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
