/**
 * Base comment types for shared usage across entities
 * @module shared/api/models/common/comment
 *
 * These types are extracted from entities/forum to allow other entities
 * (like game) to reference Comment without creating entities→entities imports.
 *
 * FSD Rule: entities should import from shared, not from each other.
 */

import type { User } from "./user";
import type { Id, Served } from "@/shared/api/models";

// ============================================================================
// Comment Types
// ============================================================================

export type CommentId = Id<string>;

/**
 * Base comment DTO used across forum topics, game comments, blog comments, etc.
 */
export type Comment = {
  id: Served<CommentId>;
  author: Served<User>;
  createdUtc: Served<string>;
  modifiedUtc: Served<string | null>;
  text: string;
  /**
   * Never sent by the API: no endpoint returns a removed comment, and the
   * soft-delete filter is global. The stores set it after a successful delete
   * so the entry becomes a placeholder instead of vanishing under the reader;
   * a reload drops the comment entirely, which is the server's answer.
   *
   * Served nonetheless, because that is what keeps a field out of Post and
   * Patch — this one belongs in no request body either. Write it through
   * markRemoved rather than casting at the call site.
   */
  isRemoved: Served<boolean>;
  likes: Served<User[]>;
};

/**
 * The local "this one was just deleted" marker.
 *
 * A branded field cannot be assigned from outside without a cast, and three
 * stores were each writing their own. One is enough, and it is the place to
 * read about why the field exists.
 */
export function markRemoved<T extends Comment>(comment: T): T {
  return { ...comment, isRemoved: true as Comment["isRemoved"] };
}
