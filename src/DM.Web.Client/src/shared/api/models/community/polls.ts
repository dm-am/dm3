import type { Id, Served, UserRef } from "../common";

export type PollId = Id<string>;
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

export type PollOptionId = Id<string>;
export type PollOption = {
  id: Served<PollOptionId>;
  text: string;
  votesCount: Served<number>;
  /**
   * Optional on the wire, not just nullable: the API omits null fields
   * (DefaultIgnoreCondition = WhenWritingNull), so an anonymous poll sends
   * neither of the three fields below at all.
   */
  voted?: Served<boolean | null>;
  /** Users who voted for this option (absent for anonymous polls, max 15) */
  voters?: UserRef[] | null;
  /** Total voters count if it exceeds 15 (absent otherwise) */
  totalVoters?: number | null;
};

/**
 * Poll status (computed by server from StartsUtc and EndsUtc)
 * Must match C# PollStatus enum values (PascalCase)
 */
export enum PollStatus {
  Pending = "Pending",
  Active = "Active",
  Closed = "Closed",
}

/**
 * Sort field options for polls
 */
export type PollSortBy = "starts" | "ends" | "status";

/**
 * API search parameters for polls
 */
export interface PollsSearchParams {
  status?: PollStatus;
  search?: string;
  startsFromUtc?: string;
  startsToUtc?: string;
  endsFromUtc?: string;
  endsToUtc?: string;
  sortBy?: PollSortBy;
  sortOrder?: "asc" | "desc";
  number?: number;
  size?: number;
  /** Filter by poll type: true for anonymous, false for public */
  isAnonymous?: boolean;
}
