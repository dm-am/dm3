import type { Served, UserRef } from "../common";
import type { SortDirection } from "@/shared/lib/filters/types";

export type PollId = Served<string>;
export type Poll = {
  id: Served<PollId>;
  startsUtc: string;
  endsUtc: string;
  title: string;
  details: string | null;
  status: Served<PollStatus>;
  isAnonymous: boolean;
  options: PollOption[];
};

export type PollOptionId = Served<string>;
export type PollOption = {
  id: Served<PollOptionId>;
  text: string;
  votesCount: Served<number>;
  /** Optional, not just nullable: an anonymous poll sends none of the three (WhenWritingNull). */
  voted?: Served<boolean | null>;
  /** Users who voted for this option (absent for anonymous polls, max 15) */
  voters?: UserRef[] | null;
  /** Total voters count if it exceeds 15 (absent otherwise) */
  totalVoters?: number | null;
};

/** The body of a new poll: the server gives each answer its id, so an answer goes as its text. */
export type CreatePollRequest = {
  title: string;
  details?: string | null;
  startsUtc: string;
  endsUtc: string;
  options: string[];
  /** Absent reads as anonymous. */
  isAnonymous?: boolean;
};

/** The body of an edit: an absent field stays as it is, and the answers are not edited. */
export type UpdatePollRequest = {
  title?: string;
  /** An empty string clears the description; absent or null leaves it. */
  details?: string | null;
  startsUtc?: string;
  endsUtc?: string;
  isAnonymous?: boolean;
};

/** Computed by the server from StartsUtc and EndsUtc; matches the C# PollStatus. */
export enum PollStatus {
  Pending = "Pending",
  Active = "Active",
  /** @public The server sends it; no code names it. */ Closed = "Closed",
}

export type PollSortBy = "starts" | "ends" | "status";

export interface PollsSearchParams {
  status?: PollStatus;
  search?: string;
  startsFromUtc?: string;
  startsToUtc?: string;
  endsFromUtc?: string;
  endsToUtc?: string;
  sortBy?: PollSortBy;
  sortOrder?: SortDirection;
  number?: number;
  size?: number;
  /** Filter by poll type: true for anonymous, false for public */
  isAnonymous?: boolean;
}
