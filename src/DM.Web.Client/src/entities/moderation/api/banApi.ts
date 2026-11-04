import type { Username } from "@/shared/api/models/community";
import type {
  ListEnvelope,
  PagingQuery,
  UserRef,
} from "@/shared/api/models/common";
import { Api } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

export default new (class BanApi {
  /** Every issue and every lift is an entry of its own: the page counts entries, not bans. */
  public getBanHistory(query?: BansQuery) {
    return Api.get<ListEnvelope<BanLogEntry>>("bans/history", query);
  }

  /** The type and the period only, for every caller, a moderator too. */
  public getBans(username: Username) {
    return Api.get<PublicUserBanStatus>(`users/${username}/bans`);
  }

  // The reason is required: the ban list writes the lift as an entry of its own.
  // The reason goes in the body: addresses end up in logs and referrers.
  public liftBan(banId: string, reason: string, options?: RequestOptions) {
    return Api.delete(`bans/${banId}`, { reason }, options);
  }
})();

/** What a ban forbids: the ordinary ("демократический") one, or everything. */
export type BanAccessPolicy = "DemocraticBan" | "FullBan";

/** With the comment and the moderator; a profile's public view is {@link PublicBan}. */
export type Ban = {
  id: string;
  /** The banned user; absent for an account that is gone. */
  user?: UserRef | null;
  /** Who issued it; absent for an account that is gone. */
  moderator?: UserRef | null;
  type: BanType;
  /** Sent so the list names the scope in words instead of guessing it. */
  accessPolicy: BanAccessPolicy;
  startedUtc: string;
  expiresUtc?: string | null;
  /** Why, rendered: the ban window takes it in the BBCode editor. */
  comment: string;
  isActive: boolean;
  /** Answered by the rule that refuses the lift itself. */
  canLift: boolean;
  liftedUtc?: string | null;
  /** Who lifted the ban early; absent while it runs, or for an account that is gone. */
  liftedBy?: UserRef | null;
  /** Why the ban was lifted early, as typed: a plain field. */
  liftReason?: string | null;
};

/** A lifted ban gives two entries carrying the same ban, a comment for each. */
export type BanLogEntry = {
  /** Counted from the oldest, so it never changes, in the list of one person too. */
  number: number;
  kind: "Issued" | "Lifted";
  occurredUtc: string;
  /** The ban as it stands now; its end is the end it had, lifted or not. */
  ban: Ban;
};

type BansQuery = PagingQuery & {
  /** A name nobody carries is an empty page. */
  username?: string;
  /** Asks for the page of this entry instead; the answer's `paging.skip` says which. */
  entry?: number;
};

export enum BanType {
  Temporary = "Temporary",
  Permanent = "Permanent",
  Voluntary = "Voluntary",
}

export type PublicBan = {
  type: BanType;
  startedUtc: string;
  expiresUtc?: string;
  isActive: boolean;
};

export type PublicUserBanStatus = {
  username: string;
  isBanned: boolean;
  activeBan?: PublicBan;
  history: PublicBan[];
};
