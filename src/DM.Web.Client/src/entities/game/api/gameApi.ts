// Game API
// Migrated from api/requests/gameApi.ts

import type {
  ListEnvelope,
  PagingQuery,
  Comment,
  User,
} from "@/shared/api/models/common";
import type {
  Game,
  GameRef,
  GameUser,
  AttributeSchema,
  Tag,
  Character,
  Room,
  Post,
  Invitation,
  PostReview,
  FirstUnreadPostResult,
  FirstUnreadCommentResult,
} from "../model/types";
import type { GameReview } from "@/shared/api/models/game/reviews";
import { Api } from "@/shared/api";

/**
 * Search params for games API
 *
 * Logic:
 * - status: Single status filter (converted to statuses array for API)
 * - recruitmentFilter: Sub-filter for Active (open/initial/subsequent/closed)
 * - closedReasonFilter: Sub-filter for Closed (None/Finished/Frozen)
 * - requiredTags: AND (game must have ALL)
 * - excludedTags: NOR (game must have NONE)
 * - masterUsernames: OR (games by any of these masters)
 * - assistantUsernames: OR (games with any of these assistants)
 */
export interface GamesSearchParams {
  search?: string;

  /** Status filter (single) */
  status?: string;

  /** Recruitment filter for Active games */
  recruitmentFilter?: "open" | "initial" | "subsequent" | "closed";

  /** Closed reason filter for Closed games */
  closedReasonFilter?: "None" | "Finished" | "Frozen";

  /** Required tag IDs - AND logic */
  requiredTags?: number[];

  /** Excluded tag IDs - NOR logic */
  excludedTags?: number[];

  /** Master usernames filter (OR logic) */
  masterUsernames?: string[];

  /** Assistant usernames filter (OR logic) */
  assistantUsernames?: string[];

  /** Hosts filter - master OR assistant (OR logic) */
  hostUsernames?: string[];

  /** Created date range start (ISO string) */
  createdFromUtc?: string;

  /** Created date range end (ISO string) */
  createdToUtc?: string;

  /** Activated date range start (ISO string) */
  activatedFromUtc?: string;

  /** Activated date range end (ISO string) */
  activatedToUtc?: string;

  /** Closed date range start (ISO string) */
  closedFromUtc?: string;

  /** Closed date range end (ISO string) */
  closedToUtc?: string;

  /** Recruitment started date range start (ISO string) */
  recruitmentStartedFromUtc?: string;

  /** Recruitment started date range end (ISO string) */
  recruitmentStartedToUtc?: string;

  /** Sort field */
  sortBy?: string;

  /** Sort order */
  sortOrder?: string;

  /** Page number (1-indexed entity position) */
  number?: number;

  /** Page size (items per page) */
  size?: number;
}

class GameApi {
  /**
   * Unified search method for games with full filter support
   */
  public searchGames(params: GamesSearchParams = {}) {
    const queryParams: Record<
      string,
      string | number | string[] | number[] | undefined
    > = {};

    if (params.search) queryParams.search = params.search;
    // Convert single status to statuses array for backend
    if (params.status) queryParams.statuses = [params.status];
    if (params.recruitmentFilter)
      queryParams.recruitmentFilter = params.recruitmentFilter;
    if (params.closedReasonFilter)
      queryParams.closedReasonFilter = params.closedReasonFilter;
    if (params.requiredTags?.length)
      queryParams.requiredTags = params.requiredTags;
    if (params.excludedTags?.length)
      queryParams.excludedTags = params.excludedTags;
    if (params.masterUsernames?.length)
      queryParams.masterUsernames = params.masterUsernames;
    if (params.assistantUsernames?.length)
      queryParams.assistantUsernames = params.assistantUsernames;
    // Map hostUsernames to authorUsernames for backend
    if (params.hostUsernames?.length)
      queryParams.authorUsernames = params.hostUsernames;

    // Date range filters
    if (params.createdFromUtc) queryParams.createdFrom = params.createdFromUtc;
    if (params.createdToUtc) queryParams.createdTo = params.createdToUtc;
    if (params.activatedFromUtc) queryParams.activatedFrom = params.activatedFromUtc;
    if (params.activatedToUtc) queryParams.activatedTo = params.activatedToUtc;
    if (params.closedFromUtc) queryParams.closedFrom = params.closedFromUtc;
    if (params.closedToUtc) queryParams.closedTo = params.closedToUtc;
    if (params.recruitmentStartedFromUtc)
      queryParams.recruitmentStartedFrom = params.recruitmentStartedFromUtc;
    if (params.recruitmentStartedToUtc)
      queryParams.recruitmentStartedTo = params.recruitmentStartedToUtc;

    // Always send sortBy and sortOrder to ensure consistent ordering
    queryParams.sortBy = params.sortBy || "created";
    queryParams.sortOrder = params.sortOrder || "desc";

    // Page size (defaults to 20 if not specified)
    const pageSize = params.size || 20;
    queryParams.take = pageSize;

    // Convert page number to skip (number is 1-indexed page)
    if (params.number && params.number > 1) {
      queryParams.skip = (params.number - 1) * pageSize;
    }

    return Api.get<ListEnvelope<Game>>("games", queryParams);
  }

  /**
   * Get games where current user participates (master, mentor, assistant, player, or reader)
   * Uses lightweight GameRef projection for sidebar efficiency
   */
  public getParticipatingGames() {
    return Api.get<ListEnvelope<GameRef>>("games", {
      participating: true,
      projection: "ref",
    });
  }

  public getModerationGames() {
    return Api.get<ListEnvelope<Game>>("games", { statuses: ["Draft"] });
  }

  /**
   * Get active games for sidebar (lightweight refs)
   */
  public getActiveGames() {
    return Api.get<ListEnvelope<GameRef>>("games", {
      statuses: ["Active"],
      recruitmentFilter: "closed",
      take: 5,
      projection: "ref",
    });
  }

  /**
   * Get recruiting games for sidebar (lightweight refs)
   */
  public getRecruitingGames() {
    return Api.get<ListEnvelope<GameRef>>("games", {
      statuses: ["Active"],
      recruitmentFilter: "open",
      take: 15,
      projection: "ref",
    });
  }

  /**
   * Get finished games for sidebar (lightweight refs)
   */
  public getFinishedGames() {
    return Api.get<ListEnvelope<GameRef>>("games", {
      statuses: ["Closed"],
      closedReasonFilter: "Finished",
      take: 5,
      projection: "ref",
    });
  }

  public getGamesByMaster(username: string) {
    return Api.get<ListEnvelope<Game>>("games", {
      masterUsernames: [username],
    });
  }

  public getGamesByPlayer(username: string) {
    return Api.get<ListEnvelope<Game>>("games", { playerUsername: username });
  }

  /**
   * Get popular games sorted by subscriber count (lightweight refs for sidebar)
   */
  public getPopularGames() {
    return Api.get<ListEnvelope<GameRef>>("games", {
      sortBy: "popularity",
      sortOrder: "desc",
      take: 10,
      projection: "ref",
    });
  }

  /**
   * Get rated posts with optional filters
   * @param params - Query parameters
   * @param params.sortBy - Sort field: rating, lastreview, created
   * @param params.sortOrder - Sort direction: asc, desc
   * @param params.hasReviews - Only posts with reviews
   * @param params.lastReviewedAfter - Posts reviewed after this ISO date
   * @param params.search - Search text in post content (ILIKE)
   * @param params.minRating - Minimum rating filter
   * @param params.gameId - Filter by game ID
   * @param params.take - Number of posts to return
   * @param params.skip - Number of posts to skip
   */
  public getRatedPosts(params?: {
    sortBy?: "rating" | "lastreview" | "reviewcount" | "created";
    sortOrder?: "asc" | "desc";
    hasReviews?: boolean;
    lastReviewedAfter?: string;
    search?: string;
    minRating?: number;
    maxRating?: number;
    authorUsernames?: string;
    createdAfter?: string;
    createdBefore?: string;
    gameId?: string;
    take?: number;
    skip?: number;
  }) {
    return Api.get<ListEnvelope<Post>>("posts", params);
  }

  public getGame(id: string) {
    return Api.get<Game>(`games/${id}/details`);
  }

  public getCharacters(gameId: string) {
    return Api.get<ListEnvelope<Character>>(`games/${gameId}/characters`);
  }

  public getRooms(gameId: string) {
    return Api.get<ListEnvelope<Room>>(`games/${gameId}/rooms`);
  }

  public getPosts(roomId: string, paging?: PagingQuery) {
    // Convert page number to skip/take for backend
    const queryParams: Record<string, number | undefined> = {};
    const pageSize = paging?.take ?? 20;
    queryParams.take = pageSize;

    if (paging?.number && paging.number > 1) {
      queryParams.skip = (paging.number - 1) * pageSize;
    } else if (paging?.skip) {
      queryParams.skip = paging.skip;
    }

    return Api.get<ListEnvelope<Post>>(`rooms/${roomId}/posts`, queryParams);
  }

  public getPost(postId: string) {
    return Api.get<Post>(`posts/${postId}`);
  }

  public createPost(roomId: string, post: Partial<Post>) {
    return Api.post<Post>(`rooms/${roomId}/posts`, post);
  }

  public updatePost(postId: string, post: Partial<Post>) {
    return Api.patch<Post>(`posts/${postId}`, post);
  }

  public deletePost(postId: string) {
    return Api.delete(`posts/${postId}`);
  }

  public createPostReview(postId: string, request: { sign: number; text: string }) {
    return Api.post<PostReview>(`posts/${postId}/reviews`, request);
  }

  public getPostReviews(postId: string, paging?: PagingQuery) {
    // Convert page number to skip/take for backend
    const queryParams: Record<string, number | undefined> = {};
    const pageSize = paging?.take ?? 20;
    queryParams.take = pageSize;

    if (paging?.number && paging.number > 1) {
      queryParams.skip = (paging.number - 1) * pageSize;
    } else if (paging?.skip) {
      queryParams.skip = paging.skip;
    }

    return Api.get<ListEnvelope<PostReview>>(`posts/${postId}/reviews`, queryParams);
  }

  // Game comments
  public getGameComments(gameId: string, paging?: PagingQuery) {
    // Convert page number to skip/take for backend
    const queryParams: Record<string, number | undefined> = {};
    const pageSize = paging?.take ?? 20;
    queryParams.take = pageSize;

    if (paging?.number && paging.number > 1) {
      queryParams.skip = (paging.number - 1) * pageSize;
    } else if (paging?.skip) {
      queryParams.skip = paging.skip;
    }

    return Api.get<ListEnvelope<Comment>>(`games/${gameId}/comments`, queryParams);
  }

  public createGameComment(gameId: string, comment: { text: string }) {
    return Api.post<Comment>(`games/${gameId}/comments`, comment);
  }

  public updateGameComment(commentId: string, comment: { text: string }) {
    return Api.patch<Comment>(`games/comments/${commentId}`, comment);
  }

  public deleteGameComment(commentId: string) {
    return Api.delete(`games/comments/${commentId}`);
  }

  // Game reviews (рецензии на игру)
  public getGameReviews(gameId: string, paging?: PagingQuery) {
    const queryParams: Record<string, number | undefined> = {};
    const pageSize = paging?.take ?? 20;
    queryParams.take = pageSize;
    if (paging?.number && paging.number > 1) {
      queryParams.skip = (paging.number - 1) * pageSize;
    } else if (paging?.skip) {
      queryParams.skip = paging.skip;
    }
    return Api.get<ListEnvelope<GameReview>>(`games/${gameId}/reviews`, queryParams);
  }

  public createGameReview(gameId: string, review: { text: string }) {
    return Api.post<GameReview>(`games/${gameId}/reviews`, review);
  }

  // Game users
  public getUsers(gameId: string) {
    return Api.get<ListEnvelope<GameUser>>(`games/${gameId}/users`);
  }

  public getAssistants(gameId: string) {
    return Api.get<ListEnvelope<GameUser>>(`games/${gameId}/users/assistants`);
  }

  public getReaders(gameId: string) {
    return Api.get<ListEnvelope<User>>(`games/${gameId}/readers`);
  }

  public subscribe(id: string) {
    return Api.post<User>(`games/${id}/readers`);
  }

  public unsubscribe(id: string) {
    return Api.delete(`games/${id}/readers`);
  }

  public getSchemas() {
    return Api.get<ListEnvelope<AttributeSchema>>("schemas");
  }

  public getTags() {
    return Api.get<ListEnvelope<Tag>>("games/tags");
  }

  public createSchema(schema: AttributeSchema) {
    return Api.post<AttributeSchema>("schemas", schema);
  }

  public createGame(game: Game) {
    return Api.post<Game>("games", game);
  }

  public createCharacter(id: string, character: Character) {
    return Api.post<Character>(`games/${id}/characters`, character);
  }

  // Invitations
  public getGameInvitations(gameId: string) {
    return Api.get<ListEnvelope<Invitation>>(`games/${gameId}/invitations`);
  }

  public invitePlayer(gameId: string, username: string) {
    return Api.post<Invitation>(`games/${gameId}/invitations/players`, {
      username,
    });
  }

  public inviteReader(gameId: string, username: string) {
    return Api.post<Invitation>(`games/${gameId}/invitations/readers`, {
      username,
    });
  }

  public cancelInvitation(gameId: string, tokenId: string) {
    return Api.delete(`games/${gameId}/invitations/${tokenId}`);
  }

  // First unread content navigation
  public getFirstUnreadPost(gameId: string) {
    return Api.get<FirstUnreadPostResult>(`games/${gameId}/posts/first-unread`);
  }

  public getFirstUnreadComment(gameId: string) {
    return Api.get<FirstUnreadCommentResult>(
      `games/${gameId}/comments/first-unread`,
    );
  }

  // Mark as read
  public markRoomAsRead(roomId: string) {
    return Api.delete(`rooms/${roomId}/posts/unread`);
  }

  public markCommentsAsRead(gameId: string) {
    return Api.delete(`games/${gameId}/comments/unread`);
  }
}

export default new GameApi();
