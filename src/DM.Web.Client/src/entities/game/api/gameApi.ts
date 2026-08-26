// Game API
// Migrated from api/requests/gameApi.ts

import type {
  Envelope,
  ListEnvelope,
  CursorEnvelope,
  PagingQuery,
  Comment,
  Message,
  QuoteSource,
  User,
} from "@/shared/api/models/common";
import type {
  CreateNotepadEntryRequest,
  UpdateNotepadEntryRequest,
} from "@/shared/api/models/notepads";
import type {
  Game,
  GameRef,
  GameUser,
  CreateGameInput,
  UpdateGameInput,
  AttributeSchema,
  Tag,
  Character,
  CharacterInput,
  CharacterStatusTransition,
  Room,
  RoomAccess,
  PostPendency,
  Post,
  Invitation,
  PostReview,
  FirstUnreadPostResult,
  FirstUnreadCommentResult,
  CreateRoomInput,
  PostPendencyInput,
  CreatePostInput,
  UpdatePostInput,
  GameStatusTransition,
  GamePremoderationTransition,
} from "../model/types";
import type { GameReview } from "@/shared/api/models/game/reviews";
import {
  Api,
  notepadEndpoints,
  toCommentsQueryParams,
  type CommentsQuery,
} from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api/audience";

/** Request options that put a read past both caches. */
const FRESH = { headers: { "Cache-Control": "no-cache" } };

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

  /** Player filter — games where this user participates as a player;
   * which participation kinds match is set by playerParticipation
   * (active characters by default) */
  playerUsername?: string;

  /**
   * Participation scope for playerUsername: "Active" (default) — only games
   * where the user has an active character; "Any" — also games where the
   * user only has retired characters or an application under review
   * (declined applications never match). Ignored without playerUsername.
   */
  playerParticipation?: "Active" | "Any";

  /**
   * If true, returns games the current authenticated user participates in
   * (any role: reader/player/assistant/mentor/master). Requires auth.
   */
  participating?: boolean;

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
      string | number | boolean | string[] | number[] | undefined
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
    if (params.hostUsernames?.length)
      queryParams.hostUsernames = params.hostUsernames;
    if (params.playerUsername)
      queryParams.playerUsername = params.playerUsername;
    if (params.playerParticipation)
      queryParams.playerParticipation = params.playerParticipation;
    if (params.participating) queryParams.participating = true;

    // Date range filters (wire names match the API query fields)
    if (params.createdFromUtc)
      queryParams.createdFromUtc = params.createdFromUtc;
    if (params.createdToUtc) queryParams.createdToUtc = params.createdToUtc;
    if (params.activatedFromUtc)
      queryParams.activatedFromUtc = params.activatedFromUtc;
    if (params.activatedToUtc)
      queryParams.activatedToUtc = params.activatedToUtc;
    if (params.closedFromUtc) queryParams.closedFromUtc = params.closedFromUtc;
    if (params.closedToUtc) queryParams.closedToUtc = params.closedToUtc;
    if (params.recruitmentStartedFromUtc)
      queryParams.recruitmentStartedFromUtc = params.recruitmentStartedFromUtc;
    if (params.recruitmentStartedToUtc)
      queryParams.recruitmentStartedToUtc = params.recruitmentStartedToUtc;

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
   * @param params.lastReviewedFromUtc - Posts reviewed at or after this ISO date
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
    lastReviewedFromUtc?: string;
    search?: string;
    minRating?: number;
    maxRating?: number;
    /** POST author usernames; repeated on the wire, one parameter each. */
    authorUsernames?: string[];
    /**
     * Restrict to posts that have at least one review by this user.
     * Used by the profile page "Оценил чужих постов".
     */
    reviewerUsername?: string;
    createdFromUtc?: string;
    createdToUtc?: string;
    gameId?: string;
    take?: number;
    skip?: number;
    number?: number;
  }) {
    return Api.get<ListEnvelope<Post>>("posts", params);
  }

  public getGame(id: string) {
    // The details endpoint wraps the payload in a single-resource envelope
    return Api.get<Envelope<Game>>(`games/${id}/details`);
  }

  public getCharacters(gameId: string) {
    return Api.get<ListEnvelope<Character>>(`games/${gameId}/characters`);
  }

  /**
   * Get a single character with its filled-in sheet, for reading. The default
   * Display audience is what separates it from getCharacterForEdit below:
   * BbCode attribute values arrive as server-rendered HTML in valueBbText, so
   * the page binds them through ContentText and never renders raw markup.
   */
  public getCharacter(characterId: string) {
    return Api.get<Envelope<Character>>(`characters/${characterId}`);
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

  /**
   * One post (GET v1/posts/{id}), enveloped like every other single-resource
   * answer. It was typed as a bare Post while the wire carried
   * `{ resource: ... }`, which nothing noticed while nobody read the body.
   */
  public getPost(postId: string) {
    return Api.get<Envelope<Post>>(`posts/${postId}`);
  }

  /**
   * Fetch a post's source for the editor. The AuthorEdit audience keeps
   * [private] and [mod] in a form the editor can round-trip; the display
   * rendering flattens [private] into ordinary markup, so seeding the editor
   * from it dropped the block on save and published the private text to the
   * whole room.
   */
  public getPostForEdit(postId: string) {
    return Api.get<Envelope<Post>>(
      `posts/${postId}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  /**
   * Fetch the markup of a quotation of a game post.
   *
   * The server composes the whole tag, author included, already filtered for
   * whoever is asking. The client does not build one out of the rendered page:
   * that conversion is lossy, and the source of somebody else's message is not
   * something the browser holds.
   */
  public getPostQuote(postId: string) {
    return Api.get<Envelope<QuoteSource>>(`posts/${postId}/quote`);
  }

  /**
   * Create a post (POST v1/rooms/{id}/posts).
   *
   * Enveloped, like every other single-resource answer in this API — it was
   * typed as a bare Post while the wire carried `{ resource: ... }`, so the one
   * caller that needed the new post's id would have read undefined off it. The
   * id is what the attachment upload names as its target.
   */
  public createPost(roomId: string, post: CreatePostInput) {
    return Api.post<Envelope<Post>>(`rooms/${roomId}/posts`, post);
  }

  /**
   * Edit a post's text (PATCH v1/posts/{id}). Backend gates the change to the
   * author or a global moderator+ (PostIntention.EditText); the 15-minute
   * author window is a client-side affordance mirrored in GamePost. The body
   * is a partial Post — only gameText/metagameText are round-tripped.
   */
  public updatePost(postId: string, patch: UpdatePostInput) {
    return Api.patch<Envelope<Post>>(`posts/${postId}`, patch);
  }

  /** Soft-delete a post (author or moderator+; PostIntention.Delete). */
  public deletePost(postId: string) {
    return Api.delete(`posts/${postId}`);
  }

  public createPostReview(
    postId: string,
    request: { sign: number; text: string },
  ) {
    return Api.post<Envelope<PostReview>>(`posts/${postId}/reviews`, request);
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

    return Api.get<ListEnvelope<PostReview>>(
      `posts/${postId}/reviews`,
      queryParams,
    );
  }

  /**
   * Fetch one review's source for the editor. The listing carries the display
   * rendering — server-built HTML — and seeding an editor from it would save
   * markup back as text; the AuthorEdit audience is the author's own view of
   * what they wrote, the same one the post and comment editors ask for.
   */
  public getPostReviewForEdit(postId: string, reviewId: string) {
    return Api.get<Envelope<PostReview>>(
      `posts/${postId}/reviews/${reviewId}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  /**
   * Edit a review (PATCH v1/posts/{postId}/reviews/{reviewId}). The backend
   * gates it to the author inside a 15-minute window
   * (PostReviewIntention.Edit + PostReviewService); PostReviewItem mirrors both
   * halves of that check on the button. Fields left out keep their value.
   */
  public updatePostReview(
    postId: string,
    reviewId: string,
    patch: { sign?: number; text?: string },
  ) {
    return Api.patch<Envelope<PostReview>>(
      `posts/${postId}/reviews/${reviewId}`,
      patch,
    );
  }

  /** Soft-delete a review (author any time, or senior moderator+). */
  public deletePostReview(postId: string, reviewId: string) {
    return Api.delete(`posts/${postId}/reviews/${reviewId}`);
  }

  // Game comments
  public getGameComments(gameId: string, query?: CommentsQuery) {
    return Api.get<ListEnvelope<Comment>>(
      `games/${gameId}/comments`,
      toCommentsQueryParams(query),
    );
  }

  public createGameComment(gameId: string, comment: { text: string }) {
    return Api.post<Envelope<Comment>>(`games/${gameId}/comments`, comment);
  }

  public updateGameComment(id: string, comment: { text: string }) {
    return Api.patch<Envelope<Comment>>(`games/comments/${id}`, comment);
  }

  public deleteGameComment(id: string) {
    return Api.delete(`games/comments/${id}`);
  }

  /**
   * Fetch a comment's raw BBCode source for the editor (AuthorEdit audience
   * round-trips [private]/[mod] for the author).
   */
  public getGameCommentForEdit(id: string) {
    return Api.get<Envelope<Comment>>(
      `games/comments/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  /**
   * Fetch the markup of a quotation of a game comment.
   *
   * The server composes the whole tag, author included, already filtered for
   * whoever is asking. The client does not build one out of the rendered page:
   * that conversion is lossy, and the source of somebody else's message is not
   * something the browser holds.
   */
  public getGameCommentQuote(id: string) {
    return Api.get<Envelope<QuoteSource>>(`games/comments/${id}/quote`);
  }

  public likeGameComment(id: string) {
    return Api.post<Envelope<User>>(`games/comments/${id}/likes`);
  }

  public unlikeGameComment(id: string) {
    return Api.delete(`games/comments/${id}/likes`);
  }

  // Game reviews (reviews of the game itself)
  public getGameReviews(gameId: string, paging?: PagingQuery) {
    const queryParams: Record<string, number | undefined> = {};
    const pageSize = paging?.take ?? 20;
    queryParams.take = pageSize;
    if (paging?.number && paging.number > 1) {
      queryParams.skip = (paging.number - 1) * pageSize;
    } else if (paging?.skip) {
      queryParams.skip = paging.skip;
    }
    return Api.get<ListEnvelope<GameReview>>(
      `games/${gameId}/reviews`,
      queryParams,
    );
  }

  public createGameReview(gameId: string, review: { text: string }) {
    return Api.post<Envelope<GameReview>>(`games/${gameId}/reviews`, review);
  }

  // Game users
  public getUsers(gameId: string) {
    return Api.get<ListEnvelope<GameUser>>(`games/${gameId}/users`);
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

  /**
   * The public tag catalogue.
   *
   * @param fresh Skip the caches — for a reload after a moderator edited the
   * catalogue. The endpoint answers `public, max-age=300` and is written
   * through `moderation/tags`, a different address, so nothing invalidates the
   * stored copy of this one.
   */
  public getTags(fresh = false) {
    return Api.get<ListEnvelope<Tag>>(
      "games/tags",
      undefined,
      undefined,
      fresh ? FRESH : undefined,
    );
  }

  public createSchema(schema: AttributeSchema) {
    // The create endpoint wraps the payload in a single-resource envelope
    return Api.post<Envelope<AttributeSchema>>("schemas", schema);
  }

  public updateSchema(id: string, schema: Partial<AttributeSchema>) {
    return Api.patch<Envelope<AttributeSchema>>(`schemas/${id}`, schema);
  }

  public createGame(game: CreateGameInput) {
    // The create endpoint wraps the payload in a single-resource envelope
    return Api.post<Envelope<Game>>("games", game);
  }

  public deleteGame(id: string) {
    return Api.delete(`games/${id}`);
  }

  public createCharacter(id: string, character: CharacterInput) {
    return Api.post<Envelope<Character>>(`games/${id}/characters`, character);
  }

  public updateCharacter(characterId: string, character: CharacterInput) {
    return Api.patch<Envelope<Character>>(
      `characters/${characterId}`,
      character,
    );
  }

  /**
   * Get a single character with its attributes for editing. AuthorEdit
   * audience: BBCode attribute values come back as raw BBCode source (in
   * valueBbText) instead of display HTML, so they can seed the editor and
   * round-trip safely.
   */
  public getCharacterForEdit(characterId: string) {
    return Api.get<Envelope<Character>>(
      `characters/${characterId}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  /**
   * Move a character to another place in the game: accept or decline an
   * application, retire it by death, exile or departure, or bring it back.
   *
   * The caller names the transition rather than a target status, because Retired
   * is reached three ways and each is a different person's right. The form is not
   * involved: this used to be a PATCH of the whole character, which meant resending
   * the name to satisfy validation and taking care not to round-trip the
   * server-rendered attribute values.
   */
  public changeCharacterStatus(
    characterId: string,
    transition: CharacterStatusTransition,
  ) {
    return Api.post<Envelope<Character>>(`characters/${characterId}/status`, {
      transition,
    });
  }

  /** Soft-delete a character (master or owner). */
  public deleteCharacter(characterId: string) {
    return Api.delete(`characters/${characterId}`);
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

  public inviteAssistant(gameId: string, username: string) {
    return Api.post<Invitation>(`games/${gameId}/invitations/assistants`, {
      username,
    });
  }

  public cancelInvitation(gameId: string, tokenId: string) {
    return Api.delete(`games/${gameId}/invitations/${tokenId}`);
  }

  // First unread content navigation.
  // The endpoints bind the game id as a Guid — callers must pass game.id
  // (not publicId). Responses are single-resource envelopes.
  public getFirstUnreadPost(gameId: string) {
    return Api.get<Envelope<FirstUnreadPostResult>>(
      `games/${gameId}/posts/first-unread`,
    );
  }

  public getFirstUnreadComment(gameId: string) {
    return Api.get<Envelope<FirstUnreadCommentResult>>(
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

  // === Chat rooms (message-based OOC rooms, cursor pagination) ===

  /**
   * Get chat room messages with CURSOR pagination (not skip/take).
   * Pass the CursorPaging.nextCursor/prevCursor from a prior page to walk.
   */
  public getChatMessages(id: string, cursor?: string, limit: number = 50) {
    return Api.get<CursorEnvelope<Message>>(`chat-rooms/${id}/messages`, {
      cursor,
      limit,
    });
  }

  public sendChatMessage(id: string, text: string) {
    return Api.post<Envelope<Message>>(`chat-rooms/${id}/messages`, { text });
  }

  public markChatRoomRead(id: string) {
    return Api.delete(`chat-rooms/${id}/messages/unread`);
  }

  // === Notepad of the game itself, "Заметки игры" (GameNotepadController) ===

  private notepadOf(gameId: string) {
    return notepadEndpoints(`games/${gameId}/notepad`);
  }

  public getNotepad(gameId: string) {
    return this.notepadOf(gameId).list();
  }

  public createNote(gameId: string, input: CreateNotepadEntryRequest) {
    return this.notepadOf(gameId).create(input);
  }

  public updateNote(
    gameId: string,
    entryId: string,
    input: UpdateNotepadEntryRequest,
  ) {
    return this.notepadOf(gameId).update(entryId, input);
  }

  public deleteNote(gameId: string, entryId: string) {
    return this.notepadOf(gameId).remove(entryId);
  }

  // === The two notepads of a character (CharacterController) ===
  //
  // Same shape, opposite audiences, and that is why they are two addresses and
  // not one with a flag: "Заметки игрока" under /notepad belong to the player
  // who owns the character and are the one notepad of a game its master cannot
  // open; "Заметки мастера" under /master-notepad belong to the master and the
  // assistants and are closed to that player. Every character has both.

  private characterNotepadOf(characterId: string) {
    return notepadEndpoints(`characters/${characterId}/notepad`);
  }

  private characterMasterNotepadOf(characterId: string) {
    return notepadEndpoints(`characters/${characterId}/master-notepad`);
  }

  public getCharacterNotepad(characterId: string) {
    return this.characterNotepadOf(characterId).list();
  }

  public createCharacterNote(
    characterId: string,
    input: CreateNotepadEntryRequest,
  ) {
    return this.characterNotepadOf(characterId).create(input);
  }

  public updateCharacterNote(
    characterId: string,
    entryId: string,
    input: UpdateNotepadEntryRequest,
  ) {
    return this.characterNotepadOf(characterId).update(entryId, input);
  }

  public deleteCharacterNote(characterId: string, entryId: string) {
    return this.characterNotepadOf(characterId).remove(entryId);
  }

  public getCharacterMasterNotepad(characterId: string) {
    return this.characterMasterNotepadOf(characterId).list();
  }

  public createCharacterMasterNote(
    characterId: string,
    input: CreateNotepadEntryRequest,
  ) {
    return this.characterMasterNotepadOf(characterId).create(input);
  }

  public updateCharacterMasterNote(
    characterId: string,
    entryId: string,
    input: UpdateNotepadEntryRequest,
  ) {
    return this.characterMasterNotepadOf(characterId).update(entryId, input);
  }

  public deleteCharacterMasterNote(characterId: string, entryId: string) {
    return this.characterMasterNotepadOf(characterId).remove(entryId);
  }

  // === Game blacklist ===

  public getBlacklist(gameId: string) {
    return Api.get<ListEnvelope<User>>(`games/${gameId}/blacklist`);
  }

  public addToBlacklist(gameId: string, username: string) {
    return Api.post<Envelope<User>>(`games/${gameId}/blacklist`, { username });
  }

  public removeFromBlacklist(gameId: string, login: string) {
    return Api.delete(`games/${gameId}/blacklist/${login}`);
  }

  // === Room CRUD & accesses ===

  public createRoom(gameId: string, room: CreateRoomInput) {
    return Api.post<Envelope<Room>>(`games/${gameId}/rooms`, room);
  }

  public updateRoom(id: string, room: Partial<Room>) {
    return Api.patch<Envelope<Room>>(`rooms/${id}`, room);
  }

  public deleteRoom(id: string) {
    return Api.delete(`rooms/${id}`);
  }

  /**
   * Archive a room. The backend Room.Type is nullable, so an omitted Type on
   * this partial PATCH leaves the stored value unchanged — we send only the
   * IsArchived flag.
   */
  public archiveRoom(id: string) {
    return Api.patch<Envelope<Room>>(`rooms/${id}`, { isArchived: true });
  }

  public unarchiveRoom(id: string) {
    return Api.patch<Envelope<Room>>(`rooms/${id}`, { isArchived: false });
  }

  public createRoomAccess(roomId: string, access: Partial<RoomAccess>) {
    return Api.post<Envelope<RoomAccess>>(`rooms/${roomId}/accesses`, access);
  }

  public deleteRoomAccess(accessId: string) {
    return Api.delete(`rooms/accesses/${accessId}`);
  }

  // === Post pendencies (turn-tracking) ===

  public createPendency(roomId: string, pendency: PostPendencyInput) {
    return Api.post<Envelope<PostPendency>>(
      `rooms/${roomId}/pendencies`,
      pendency,
    );
  }

  public deletePendency(pendencyId: string) {
    return Api.delete(`rooms/pendencies/${pendencyId}`);
  }

  // === Game users (Master-only removal) ===

  public removeAssistant(gameId: string, username: string) {
    return Api.delete(`games/${gameId}/users/assistants/${username}`);
  }

  // === Game mutations ===

  public updateGame(id: string, patch: UpdateGameInput) {
    return Api.patch<Envelope<Game>>(`games/${id}/details`, patch);
  }

  public transitionStatus(id: string, transition: GameStatusTransition) {
    return Api.post<Envelope<Game>>(`games/${id}/status`, { transition });
  }

  /**
   * Premoderation transition (POST v1/games/{id}/premoderation). The endpoint
   * is authentication-gated and checks the rank per move: SetApproved and
   * SetAwaitingEdits are Mentor+, SubmitForApproval belongs to the master alone.
   */
  public changePremoderation(
    id: string,
    transition: GamePremoderationTransition,
  ) {
    return Api.post<Envelope<Game>>(`games/${id}/premoderation`, {
      transition,
    });
  }

  public resetRecruitment(id: string) {
    return Api.post<Envelope<Game>>(`games/${id}/reset-recruitment-date`);
  }
}

export default new GameApi();
