// In shared so that entities reference Comment without importing each other.

import type { User } from "./user";
import type { Served } from "@/shared/api/models";

export type CommentId = Served<string>;

/** The comment of a topic, game, blog or publication. */
export type Comment = {
  id: Served<CommentId>;
  /** Null when the author's account was removed: the comment stays, unsigned. */
  author: Served<User | null>;
  createdUtc: Served<string>;
  modifiedUtc: Served<string | null>;
  text: string;
  /**
   * Never sent: the store sets it after a delete so the entry stays as a
   * placeholder until reload. Served keeps it out of Post and Patch; write it
   * through markRemoved.
   */
  isRemoved: Served<boolean>;
  likes: Served<User[]>;
};

/** The local "just deleted" marker: a branded field cannot be set without a cast. */
export function markRemoved<T extends Comment>(comment: T): T {
  return { ...comment, isRemoved: true as Comment["isRemoved"] };
}
