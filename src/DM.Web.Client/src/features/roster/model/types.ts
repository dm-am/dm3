import type { User, UserRef } from "@/entities/user";

export interface BlacklistRow {
  id: string;
  username: string;
  /** The user as the game and the blog hand him, role badge and all. */
  user?: User | UserRef;
  /** Only the account's list carries it. */
  createdUtc?: string;
}
