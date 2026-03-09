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
  updatedUtc: Served<string | null>;
  text: string;
  isRemoved: Served<boolean>;
  likes: Served<User[]>;
};
