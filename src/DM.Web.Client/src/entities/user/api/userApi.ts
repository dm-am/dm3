import type {
  Envelope,
  ListEnvelope,
  PagingQuery,
} from "@/shared/api/models/common";
import type {
  User,
  UserProfile,
  Username,
  UserRole,
  UserProfileNote,
  UserEndorsement,
  EndorsementEligibility,
  CreateUserEndorsementRequest,
} from "@/shared/api/models/community";
import { UserActivityFilter } from "@/shared/api/models/community";
import type { GameReview } from "@/shared/api/models/game/reviews";
import { Api } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

/** `sortBy: "author"` is the other side of the pair: the author, or the game for those written. */
export type GameReviewsQuery = PagingQuery & {
  search?: string;
  sortBy?: "created" | "author";
  sortOrder?: "asc" | "desc";
};

/** `sortBy: "author"` is the other side of the pair: the author, or the recipient for those written. */
export type EndorsementsQuery = GameReviewsQuery;

// The server does not bind `number` and ignores an unknown parameter, so the page becomes skip.
function buildListParams(q: GameReviewsQuery | undefined, defaultTake: number) {
  const params: Record<string, string | number | undefined> = {};
  if (!q) return params;
  const pageSize = q.take ?? defaultTake;
  params.take = pageSize;
  if (q.number && q.number > 1) {
    params.skip = (q.number - 1) * pageSize;
  } else if (q.skip != null) {
    params.skip = q.skip;
  }
  if (q.search) params.search = q.search;
  if (q.sortBy) params.sortBy = q.sortBy;
  if (q.sortOrder) params.sortOrder = q.sortOrder;
  return params;
}

// The directory and what hangs off a public profile; the viewer's own account is elsewhere.
export default new (class UserApi {
  /** Every key is forwarded; an explicit `skip` wins over `number`. */
  public getUsers(
    q: Record<string, string | number | boolean | undefined> & {
      number?: number;
      take?: number;
      skip?: number;
    },
  ) {
    const { number, ...rest } = q;
    const queryParams: Record<string, string | number | boolean | undefined> = {
      ...rest,
    };

    const pageSize = typeof q.take === "number" ? q.take : 20;
    queryParams.take = pageSize;

    if (queryParams.skip == null && number && number > 1) {
      queryParams.skip = (number - 1) * pageSize;
    }

    return Api.get<ListEnvelope<User>>("users", queryParams);
  }

  public searchUsers(
    search: string,
    size: number = 10,
    activity?: UserActivityFilter,
  ) {
    // Without activity the server shows the active users only.
    return Api.get<ListEnvelope<User>>("users", {
      search,
      take: size,
      activity,
    });
  }

  /** The inactive ones too: a staff roster is not a list of who is online. */
  public getUsersByRole(role: UserRole) {
    return this.getUsers({ role, activity: "All", take: 100 });
  }

  public getUser(username: Username) {
    return Api.get<Envelope<User>>(`users/${username}`);
  }

  public getUserProfile(username: Username) {
    return Api.get<Envelope<UserProfile>>(`users/${username}/profile`);
  }

  public upsertUserProfileNote(username: Username, text: string) {
    return Api.put<UserProfileNote>(`users/me/notes/${username}`, { text });
  }

  public deleteUserProfileNote(username: Username, options?: RequestOptions) {
    return Api.delete(`users/me/notes/${username}`, undefined, options);
  }

  public getUserEndorsements(username: Username, q?: EndorsementsQuery) {
    return Api.get<ListEnvelope<UserEndorsement>>(
      `users/${username}/endorsements`,
      buildListParams(q, 20),
    );
  }

  public getWrittenUserEndorsements(username: Username, q?: EndorsementsQuery) {
    return Api.get<ListEnvelope<UserEndorsement>>(
      `users/${username}/written-endorsements`,
      buildListParams(q, 20),
    );
  }

  /** Reviews of the games they master, not of posts. */
  public getUserGameReviews(username: Username, q?: GameReviewsQuery) {
    return Api.get<ListEnvelope<GameReview>>(
      `users/${username}/game-reviews`,
      buildListParams(q, 10),
    );
  }

  public getWrittenUserGameReviews(username: Username, q?: GameReviewsQuery) {
    return Api.get<ListEnvelope<GameReview>>(
      `users/${username}/written-game-reviews`,
      buildListParams(q, 10),
    );
  }

  /** The rules of the create endpoint in advance; a guest is answered 200 with false. */
  public getEndorsementEligibility(
    username: Username,
    options?: RequestOptions,
  ) {
    return Api.get<Envelope<EndorsementEligibility>>(
      `users/${username}/endorsements/eligibility`,
      undefined,
      undefined,
      options,
    );
  }

  public createUserEndorsement(
    username: Username,
    request: CreateUserEndorsementRequest,
    options?: RequestOptions,
  ) {
    return Api.post<UserEndorsement>(
      `users/${username}/endorsements`,
      request,
      options,
    );
  }
})();
