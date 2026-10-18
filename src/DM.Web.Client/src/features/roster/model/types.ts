import type { User, UserRef } from "@/entities/user";

/** One name on a blacklist of the site, as BlacklistEditor draws it. */
export interface BlacklistRow {
  id: string;
  username: string;
  /** The user as the game and the blog hand him, role badge and all. */
  user?: User | UserRef;
  /** When the name went on: the account's list says it, the others do not. */
  createdUtc?: string;
}
