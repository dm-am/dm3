// Game entity types
// Migrated from api/models/game/

import type {
  PagingQuery,
  Rating,
  User,
  UserPicture,
  UserRef,
} from "@/shared/api/models/common";
import type { Id, Served } from "@/shared/api/models";
import type {
  AttributeSpecification,
  GameAttributeSchema,
} from "@/shared/api/models/game/attributeSchemas";
import { ModuleStatus } from "@/shared/api/models/common";

// === Game Status & Roles ===

/** Game lifecycle status — an alias of the shared {@link ModuleStatus}. */
export const GameStatus = ModuleStatus;
export type GameStatus = ModuleStatus;

export enum ClosedReason {
  None = "None",
  Finished = "Finished",
  Frozen = "Frozen",
}

/**
 * Premoderation status of a game (mirrors backend PremoderationStatus, and the
 * blog type of the same shape). "Approved" means the game needs no
 * premoderation and the whole subject is hidden in the UI.
 */
export type GamePremoderationStatus =
  | "Approved"
  | "AwaitingApproval"
  | "AwaitingEdits";

export enum DraftVisibility {
  Private = "Private",
  Public = "Public",
}

/**
 * How the current user takes part in a game.
 *
 * Mirrors the API's GameParticipation flags, which is what the wire actually
 * carries. The client used to mirror the domain's site-level GameRole instead —
 * only Player and Reader overlap between the two, so a master's own games
 * matched no bucket at all and the sidebar block came out empty.
 */
export const GameParticipation = {
  /** Game master. */
  Owner: "Owner",
  /** Master or assistant. */
  Authority: "Authority",
  /** Invited as assistant, not yet accepted. */
  PendingAssistant: "PendingAssistant",
  Player: "Player",
  /** Subscribed reader. */
  Reader: "Reader",
  /** Game mentor. */
  Moderator: "Moderator",
} as const;

export type GameParticipation =
  (typeof GameParticipation)[keyof typeof GameParticipation];

// === Tags ===

export type Tag = {
  /** Numeric tag ID for filtering */
  id: number;
  title: string;
  /**
   * Tag description as the server renders it: the markup of the hint surface,
   * whose [tip] words open a small tooltip of their own (TooltipContent).
   */
  description?: string;
  groupTitle: string;
  /** Tag group description */
  groupDescription?: string;
  gamesCount: number;
  /** Tag sort order within its group */
  sortOrder: number;
  /** Tag group sort order */
  groupSortOrder: number;
  /**
   * How many tags of this tag's group one game may carry. Absent means the
   * group limits nothing. Kept in the catalog moderation edits, so the picker
   * reads it from the server instead of holding a copy of the numbers.
   */
  groupMaxTagsPerGame?: number | null;
};

// === Game Settings ===

/**
 * Who may write in the discussion of a game; everybody may read it. DM2's
 * "Доступно всем без ограничений" and "Доступно посторонним только для
 * чтения" (the people without a role in the game read and do not write).
 */
export enum CommentariesAccessMode {
  Public = "Public",
  Readonly = "Readonly",
}

export interface GamePrivacySettings {
  viewPrivates: boolean;
  viewDice: boolean;
  commentariesAccess: CommentariesAccessMode;
}

export interface GameRecruitment {
  isOpen: boolean;
  pcLimit?: number;
  pcCount: number;
  /** When recruitment was started (ISO date string) */
  startedUtc?: string;
  /** Whether this is a subsequent recruitment ("донабор") */
  isSubsequent: boolean;
}

// === Game ===

export type GameId = Id<string>;

// === GameRef (lightweight for sidebars/menus) ===

/**
 * Lightweight game reference for sidebars and menus.
 * Uses counts instead of user arrays for players/readers.
 * Request with ?projection=ref to get this type.
 */
export type GameRef = {
  id: Served<GameId>;
  publicId: Served<string>;
  title: string;
  status: GameStatus;
  closedReason?: ClosedReason;
  activatedUtc?: string;
  /** Null when the master's account was removed: the game stays, unsigned. */
  master: Served<UserRef | null>;
  assistants: Served<UserRef[]>;
  /** User participation flags */
  participation: Served<GameParticipation[]>;
  subscribersCount: number;
  recruitment: Served<GameRecruitment>;
  unreadPostsCount: Served<number>;
  unreadCommentsCount: Served<number>;
  /** Number of reviews about the game itself */
  gameReviewsCount: Served<number>;
  /** Number of reviews about posts in the game */
  postReviewsCount: Served<number>;
  /** Subscriber usernames for tooltip (limited to first 20) */
  subscriberUsernames?: string[];
  /** Active characters info for [X/Y] tooltip */
  activeCharacters?: ActiveCharacterInfo[];
  /**
   * The game waits for a post from the current viewer. Same expectations the
   * room list marks with a star, summed up per game by the server, so a list of
   * games shows the marker without reading every room.
   */
  awaitsViewerTurn?: boolean;
  /** Names of the viewer's characters the game waits a post for */
  awaitedCharacterNames?: string[];
};

/** Active character info for tooltip */
export interface ActiveCharacterInfo {
  /** Character name */
  name: string;
  /** Owner's username */
  ownerUsername: string;
}

/**
 * Backend character status as serialized by the API
 * (see src/DM.Domain.Core/Enums/CharacterStatus.cs). Retired is refined
 * by the isDead / isPlayerLeft / isPlayerExiled flags.
 */
export type ApiCharacterStatus =
  | "UnderReview"
  | "Declined"
  | "Active"
  | "Retired";

/**
 * Requested change to a character's place in the game
 * (see src/DM.Domain.Game/Features/Characters/CharacterStatusTransition.cs).
 *
 * The transition is named rather than the target status: Retired is reached by
 * dying, by being exiled and by leaving, and each is a different person's right.
 */
export type CharacterStatusTransition =
  | "Accept"
  | "Decline"
  | "Kill"
  | "Exile"
  | "Leave"
  | "Resurrect"
  | "Readmit";

/**
 * Character of the player targeted by the playerUsername search filter
 * (profile games table, "Игрок" mode)
 */
export interface PlayerCharacterInfo {
  /** Character name */
  name: string;
  /** Character status */
  status: ApiCharacterStatus;
  /** Character died in game (meaningful when status = Retired) */
  isDead: boolean;
  /** Player voluntarily left the game (meaningful when status = Retired) */
  isPlayerLeft: boolean;
  /** Player was exiled by the master (meaningful when status = Retired) */
  isPlayerExiled: boolean;
}

/**
 * What the viewer may do in a game (mirrors GameViewer). The server asks each
 * flag with the code that refuses the write it stands for, so a page that
 * offers by it offers only what the server takes.
 */
export interface GameViewer {
  /**
   * May write a new comment in the game's discussion: the mode with the
   * viewer's roles, the blacklist and the ban at once. False for a guest.
   */
  canComment: boolean;
  /**
   * May review the game: past the probation, with a post of theirs in it and
   * no review of theirs there yet. False for a guest.
   */
  canReview: boolean;
}

// === Game (full, for lists and tables) ===

/**
 * Full game DTO for lists and tables.
 * Extends GameRef with additional fields for display.
 *
 * Inherits from GameRef:
 * - id, title, status, closedReason, activatedUtc
 * - master, assistants, participation
 * - subscribersCount, recruitment, unreadPostsCount, unreadCommentsCount
 */
export interface Game extends GameRef {
  system: string;
  setting: string;
  closedUtc?: string;
  createdUtc: string;

  /** Full assistant details (only on game detail page) */
  fullAssistants?: Served<User[]>;
  pendingAssistant: Served<UserRef | null>;
  mentor: Served<UserRef | null>;
  /**
   * Premoderation status. Absent from older payloads, so optional: a game that
   * does not say is treated as needing nothing.
   */
  premoderationStatus?: GamePremoderationStatus;
  info: string;

  /** Full tags (only for single game details, null for lists) */
  tags?: Tag[];
  /** Tag IDs only (for lists - use cached /games/tags for descriptions) */
  tagIds: number[];
  privacySettings: GamePrivacySettings;
  /**
   * The character sheet of the game. Part of the game itself, so the details
   * answer always carries one; a list projection carries none.
   */
  schema?: GameAttributeSchema;

  /** Unique players (authors of active characters) - for game details page */
  players?: Served<UserRef[]>;

  /**
   * Game readers ("Читатели") - the users subscribed to the game. A reader is a
   * game subscriber in the domain, so this mirrors the subscriber set. Present
   * only on the game details response.
   */
  readers?: Served<UserRef[]>;

  /**
   * The viewer is on the blacklist of this game: they may not comment in its
   * discussion or apply to it, nor edit their own comment or application;
   * everything else is open to them as to anyone. False for a guest. Game
   * details only. The comment form goes by `viewer.canComment`, which says
   * the blacklist together with every other reason.
   */
  viewerBlacklisted?: Served<boolean>;

  /**
   * What the viewer may do in this game, answered by the code that refuses
   * the request. Game details only.
   */
  viewer?: Served<GameViewer>;

  /** Total posts across all rooms ("Постов всего"). Game details only. */
  totalPostsCount?: Served<number>;

  /** Posts authored by the master ("Постов мастера"). Game details only. */
  masterPostsCount?: Served<number>;

  /**
   * Timestamp of the master's most recent post ("Последний пост мастера").
   * Null/absent if the master has not posted. Game details only.
   */
  lastMasterPostUtc?: Served<string | null>;

  /**
   * Whether the game supports dice rolls ("Поддержка кубика") - true when any
   * room of the game has dice rolling enabled. Game details only.
   */
  diceSupported?: Served<boolean>;

  /**
   * Characters of the player targeted by the playerUsername search filter
   * (name + status for the profile games table). Conditional: only present
   * on list responses requested with the playerUsername filter.
   */
  playerCharacters?: PlayerCharacterInfo[];

  unreadCharactersCount: Served<number>;
}

export interface GamesQuery extends PagingQuery {
  statuses: GameStatus[];
}

// === Game creation ===

/**
 * Privacy settings submitted on game creation
 * (maps to backend CreateGamePrivacySettings).
 */
export interface CreateGamePrivacySettingsInput {
  /** Players can read private messages in posts */
  viewPrivates: boolean;
  /** Players can see dice roll results */
  viewDice: boolean;
  /** Players can see post statistics */
  viewPostStats: boolean;
  /** Access mode for game commentaries */
  commentariesAccess: CommentariesAccessMode;
}

/**
 * Payload for creating a game (maps to backend CreateGameRequest).
 * @see src/DM.Web.API/Features/Game/Games/CreateGameRequest.cs
 */
export interface CreateGameInput {
  title: string;
  /** RPG system name */
  system?: string;
  /** Narrative setting */
  setting?: string;
  /** Game description (raw BBCode source) */
  info: string;
  /**
   * Create the game in the making ("Оформляется"), out of sight of everyone
   * without a role in it; false creates it started ("Оформлен").
   */
  draft?: boolean;
  /** Game tag short identifiers, the ones /games/tags serves */
  tags?: number[];
  /**
   * The character sheet the game starts with, saved together with the game. An
   * empty set is allowed and means characters with no attributes.
   */
  attributeSpecifications?: AttributeSpecification[];
  /** Assistant username */
  assistantUsername?: string;
  privacySettings?: CreateGamePrivacySettingsInput;
}

// === Game update ===

/**
 * Editable privacy settings (maps to backend UpdateGamePrivacySettings).
 */
export interface UpdateGamePrivacySettingsInput {
  viewPrivates?: boolean;
  viewDice?: boolean;
  viewPostStats?: boolean;
  commentariesAccess?: CommentariesAccessMode;
}

/**
 * Editable recruitment settings (maps to backend UpdateGameRecruitment). The
 * read-only recruitment facts the response carries — how many characters are
 * in, when the wave started — are the server's and are not sent back.
 */
export interface UpdateGameRecruitmentInput {
  isOpen?: boolean;
  pcLimit?: number;
}

/**
 * Payload for editing a game (maps to backend UpdateGameRequest).
 *
 * Deliberately not `Partial<Game>`: the response DTO carries forty fields the
 * endpoint does not accept, and typing the patch by it invited the settings
 * form to send controls the server would silently drop.
 *
 * @see src/DM.Web.API/Features/Game/Games/UpdateGameRequest.cs
 */
export interface UpdateGameInput {
  title?: string;
  /** RPG system name */
  system?: string;
  /** Narrative setting */
  setting?: string;
  /** Game description (raw BBCode source) */
  info?: string;
  /**
   * Game tag short identifiers, the ones /games/tags serves. The whole set:
   * omit the field to leave the tags alone, send an empty list to clear them.
   */
  tags?: number[];
  privacySettings?: UpdateGamePrivacySettingsInput;
  recruitment?: UpdateGameRecruitmentInput;
}

// === Invitations ===

// Invitation types live in shared/api/models (FSD: shared must not import
// entities); the entity re-exports them as its public model surface.
export type {
  Invitation,
  InvitationType,
} from "@/shared/api/models/game/invitations";

export interface GameUser {
  user: UserRef;
  role: "master" | "mentor" | "assistant" | "player" | "reader";
  joinedUtc: string;
  characterId?: string;
  characterName?: string;
  characterStatus?: string;
}

// === Attribute Schemas ===

// The sheet shapes on the wire live in shared/api/models (FSD: shared must not
// import entities), because the template catalogue reads the same ones; the
// entity re-exports them as its public model surface.
export {
  AttributeSpecificationType,
  type AttributeValueSpecification,
  type AttributeSpecification,
  type GameAttributeSchema,
  type ReplaceGameAttributeSchemaRequest,
} from "@/shared/api/models/game/attributeSchemas";

/**
 * What the schema editor holds while the composer works on it.
 *
 * Not a wire shape: a draft has no identifier, because the game it will belong
 * to may not exist yet, and the specifications inside it carry identifiers the
 * browser made up that the server hands out again on save. The title belongs to
 * a template and to nothing else, because a sheet owned by a game is named by
 * that game, so the hosts that edit a game's sheet leave the field out.
 */
export interface AttributeSchemaDraft {
  title: string;
  specifications: AttributeSpecification[];
}

// === Characters ===

export type CharacterPrivacySettings = {
  isNpc: boolean;
  editByMaster: boolean;
  editPostByMaster: boolean;
};

export type CharacterAttributeId = Id<string>;
export type CharacterAttribute = {
  id: Served<CharacterAttributeId>;
  title: Served<string>;
  /**
   * Raw value for non-BbCode specifications (plain string). Null for BbCode
   * specifications, whose display value is carried by {@link valueBbText}.
   */
  value: string | null;
  /**
   * Server-rendered HTML for BbCode specifications. Present only when the
   * backing specification is BbCode; bind it with ContentText (never the raw
   * `value`). See docs/architecture/BBCODE_RENDERING.md.
   */
  valueBbText?: Served<string>;
  modifier: Served<string>;
  inconsistent: Served<string>;
};

/**
 * Single attribute value submitted on character create/update. Carries only
 * the specification id and the raw value; the server derives title, type and
 * (for list specs) the modifier from the schema specification.
 */
export interface CharacterAttributeInput {
  /** Specification id (AttributeSpecification.id) this value belongs to */
  id: string;
  /** Raw value; for BbCode specs this is the raw BBCode source */
  value: string;
}

/**
 * Payload for creating or updating a character (maps to the API
 * CharacterDetails DTO). Only the writable fields are carried.
 */
export interface CharacterInput {
  name: string;
  privacy: CharacterPrivacySettings;
  attributes: CharacterAttributeInput[];
}

export type CharacterId = Id<string>;
export type Character = {
  id: Served<CharacterId>;
  /** Character owner. Absent for NPC characters (controlled by the master). */
  author?: Served<UserRef>;
  /** Character status; Retired is refined by the flags below. */
  status: ApiCharacterStatus;
  /** Character died in game (meaningful when status = Retired) */
  isDead: Served<boolean>;
  /** Player voluntarily left the game (meaningful when status = Retired) */
  isPlayerLeft: Served<boolean>;
  /** Player was exiled by the master (meaningful when status = Retired) */
  isPlayerExiled: Served<boolean>;
  name: string;
  /**
   * Character avatar (3 variants). Symmetric with User.picture.
   * Null URLs if the character has no uploaded avatar.
   */
  picture: Served<UserPicture>;
  /** Character is NPC (controlled by game master) */
  isNpc: Served<boolean>;
  /**
   * Editing policy. Detail-only, like the sheet below: the roster listing does
   * not carry it, and read from a roster entry it is undefined.
   */
  privacy?: CharacterPrivacySettings;
  /**
   * The filled-in sheet. Absent from the roster listing, which answers names
   * and portraits: the sheet is read one character at a time.
   */
  attributes?: Served<CharacterAttribute[]>;
  totalPostsCount: Served<number>;
  /**
   * Timestamp of the character's most recent post ("Последний ход" column).
   * Null/absent if the character has never posted.
   */
  lastPostUtc?: Served<string | null>;
  /**
   * Descriptor attribute value ("Класс" / class column on the game main page).
   * Null/absent when the game schema has no descriptor specification or the
   * character has no value for it.
   */
  descriptor?: Served<string | null>;
  /**
   * The character author's rating ("Рейтинг" column). Null for NPC characters
   * (no author) or when the author has disabled rating display.
   */
  authorRating?: Served<Rating | null>;
};

// === Rooms & Posts ===

export type RoomId = Id<string>;

/**
 * Room content type.
 * @see src/DM.Domain.Core/Enums/RoomType.cs — the backend enum only has
 * Default (post-based) and Chat (message-based) rooms.
 */
export enum RoomType {
  /** General post-based room */
  Default = "Default",
  /** Chat room (message-based, cursor pagination) */
  Chat = "Chat",
}

export enum RoomAccessType {
  /** Anyone can view the room */
  Open = "Open",
  /** Only linked character players may view the room */
  Private = "Private",
}

/**
 * Per-room access grant policy.
 *  - ReadOnly: can view the room but cannot post
 *  - Full: can view and post (only valid for characters)
 */
export enum RoomAccessPolicy {
  ReadOnly = "ReadOnly",
  Full = "Full",
}

/**
 * Per-room access grant. For Private rooms, this enumerates:
 *  - characters whose owners may view the room (character + user);
 *    characters can have either ReadOnly or Full policy
 *  - explicit readers added to the room (user only, no character);
 *    readers are always ReadOnly
 */
export interface RoomAccess {
  id: string;
  policy?: RoomAccessPolicy;
  character?: Character | null;
  user?: UserRef | null;
}

/**
 * An expectation that some character's player writes the next post in a room
 * (DM.Web.API.Features.Game.Rooms.PostPendency). It stays in the payload once
 * answered, with fulfilledUtc set, so every reader has to filter for itself.
 * characterName is declared non-optional: the read path always carries it.
 * The create body is PostPendencyInput, the character alone.
 */
export interface PostPendency {
  id: string;
  roomId: string;
  characterId: string;
  characterName: string;
  /** Null when the account that set the expectation was removed. */
  createdBy?: UserRef | null;
  waitingFor?: UserRef | null;
  createdUtc: string;
  fulfilledUtc?: string | null;
}

export interface RoomSettings {
  viewPrivateText: boolean;
  viewDiceResults: boolean;
  diceEnabled: boolean;
  /**
   * Room is kept out of the game's room list for everybody who may not open
   * it. The server is what drops it: a hidden room never reaches a viewer
   * without access at all, so this is only ever read by the settings form.
   */
  hiddenWithoutAccess: boolean;
}

export type Room = {
  id: Served<RoomId>;
  roomNumber: Served<number>;
  title: string;
  access?: RoomAccessType;
  type?: RoomType;
  /**
   * Room is archived (hidden from the active rooms list, kept for history).
   * Absent on older payloads — treat missing as not archived.
   */
  isArchived?: boolean;
  /**
   * Viewer may open the room. The rooms listing names every room of the game,
   * closed ones included, and answers false for a private room the viewer may
   * not enter; a read that returns a room at all returns one they may, and
   * leaves this out.
   */
  canView?: boolean;
  /** Explicit per-room access grants (characters and/or readers) */
  accesses?: RoomAccess[];
  pendencies?: PostPendency[];
  unreadPostsCount: number;
  /**
   * Messages in the room, removed ones left out: the posts of a room of posts,
   * the messages of a room of chat. Zero on a room the viewer may not open.
   */
  totalPostsCount?: number;
  /**
   * The characters let in to write in a room the viewer may not open, a dead
   * or departed one included, in the order they joined the game; the menu
   * writes the room with them, "[Лес] - Густав, Эодор". Empty on a room the
   * viewer may open: its accesses say it.
   */
  characterNames?: string[];
  settings?: RoomSettings;
  /**
   * Parent game reference for this room. Post-listing endpoints
   * (rated posts, pulse) populate this with a full sidebar-tier
   * GameRef — master, assistants, active characters, recruitment,
   * counts — so GameLink renders its tooltip and RoomLink builds its
   * route without a second network round-trip per post.
   * Mirrors the sidebar's data-flow (ActiveGames, RecruitingGames,
   * OwnedGames all pass full GameRef objects straight into GameLink).
   */
  game?: GameRef;
};

/** One die of a roll: its value, and whether it exploded or came up critical. */
export interface DiceResult {
  value: number;
  exploded?: boolean;
  critical?: boolean;
}

/**
 * A roll as the server publishes it: how many dice of how many edges, the bonus,
 * and every die that fell. The total is the sum of the values plus the bonus and
 * is not sent — it is arithmetic the reader can check against the dice shown.
 *
 * This type once claimed `{ dice, result }`, a shape the API never sent, so a
 * published roll rendered as "dundefined: undefined = NaN".
 */
export interface DiceRoll {
  rolls: number;
  edges: number;
  bonus: number;
  explosion?: number | null;
  results: DiceResult[];
  comment?: string;
}

/**
 * BBCode text - the API returns this as a plain HTML string.
 * The backend converts BBCode to HTML during JSON serialization.
 */
export type PostBbText = string;

/** A single post edit-history entry (mirrors backend PostEditInfo). */
export interface PostEditInfo {
  id: string;
  modifiedUtc: string;
  /** Null when the editor's account was removed. */
  editor?: UserRef | null;
}

export type PostId = Id<string>;
export type Post = {
  id: Served<PostId>;
  room?: Room;
  character?: Character;
  /** Null when the author's account was removed: the post stays, unsigned. */
  author?: UserRef | null;
  /** Author's game role: DungeonMaster, Assistant, or null for player posts */
  authorGameRole?: "DungeonMaster" | "Assistant";
  createdUtc: string;
  /** Edit history (most recent first); presence marks an edited post. */
  edits?: PostEditInfo[];
  gameText: PostBbText;
  metagameText?: PostBbText;
  diceRolls?: DiceRoll[];
  rating?: number;
  reviewCount?: number;
  /** Files attached to the post, oldest first. */
  attachments?: PostAttachment[];
  /**
   * Place of the post in its room, in the order the room is written in. The
   * same under any filter and sort of the room, so a filtered page still says
   * where each post stands. The room's own lists and single-post reads carry
   * it; the rated feed does not.
   */
  number?: number;
  /**
   * The game the post was written in. A review of one post closes the reviews
   * of the game's other posts to the same reviewer for three days, and a page
   * holding posts of several games keeps that apart by this.
   */
  gameId?: Served<string>;
  /**
   * What the viewer may do with this post, answered by the code that refuses
   * the request. Every answer that carries posts carries it, the lists too.
   */
  viewer?: Served<PostViewer>;
};

/**
 * What the viewer may do with a post (mirrors PostViewer). The server asks the
 * flag with the code that refuses the write it stands for.
 */
export interface PostViewer {
  /**
   * May review the post: not their own, not reviewed by them already, and no
   * review of theirs in the same game during the last three days. False for a
   * guest. The probation is not in it: a newbie is offered "=".
   */
  canReview: boolean;
}

/**
 * A file attached to a post.
 *
 * `url` is an endpoint, not a link into the bucket: the server re-decides who
 * may have the bytes on every request, so passing the address on gives the
 * recipient exactly the access they already had.
 */
export interface PostAttachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  width?: number | null;
  height?: number | null;
  url: string;
  createdUtc: string;
}

/**
 * Review rating sign/sentiment
 * @see src/DM.Domain.Core/Enums/ReviewSign.cs
 */
export enum ReviewSign {
  Negative = -1,
  Neutral = 0,
  Positive = 1,
}

/**
 * post review ("оценка поста")
 * Rating with optional comment for a post
 * - BBCode supported (optional)
 * - One review per post per user
 */
export interface PostReview {
  id: string;
  postId: string;
  gameId: string;
  /** Null when the reviewer's account was removed. */
  author: UserRef | null;
  /** Null when the account that wrote the post was removed. */
  postAuthor: UserRef | null;
  text: string;
  sign: ReviewSign;
  createdUtc: string;
  modifiedUtc?: string;
}

// === Unread Results ===

/**
 * Result of finding the first unread post in a game
 */
export interface FirstUnreadPostResult {
  /** Room containing the first unread post */
  roomId: string;
  /** Post number (1-based) for pagination */
  postNumber: number;
  /** Post identifier for scroll targeting */
  postId: string;
  /** Total number of unread posts in the game */
  totalUnreadCount: number;
  /** Whether there are unread posts (or any posts for anonymous users) */
  hasUnread: boolean;
}

/**
 * Where a room opens when its address names no page: the first unread post,
 * or the last post when nothing is unread (the first one for a guest).
 */
export interface FirstUnreadRoomPostResult {
  /** Absent for a room without posts */
  postId?: string;
  /** Number of that post in the room; 0 for a room without posts */
  postNumber: number;
  /** Page holding that post at `pageSize` */
  page: number;
  pageSize: number;
  totalUnreadCount: number;
  hasUnread: boolean;
}

/**
 * What a reader asks of one room's list besides the page: the pulse filters
 * under the pulse names, plus the characters. A field left out filters nothing.
 */
export interface RoomPostsFilter {
  search?: string;
  /** created (the order the room is written in) is the server's default */
  sortBy?: "created" | "rating" | "reviewcount" | "lastreview";
  sortOrder?: "asc" | "desc";
  authorUsernames?: string[];
  characterIds?: string[];
  /** Alone: only posts without a character; with characterIds: those too */
  withoutCharacter?: boolean;
  minRating?: number;
  maxRating?: number;
  /** true: only posts with reviews; false: only posts without */
  hasReviews?: boolean;
  /** ISO 8601 */
  createdFromUtc?: string;
  /** ISO 8601 */
  createdToUtc?: string;
}

/**
 * Result of finding the first unread comment in a game
 */
export interface FirstUnreadCommentResult {
  /** Comment number (1-based) for pagination */
  commentNumber: number;
  /** Comment identifier for scroll targeting */
  commentId: string;
  /** Total number of unread comments in the game */
  totalUnreadCount: number;
  /** Whether there are unread comments (or any comments for anonymous users) */
  hasUnread: boolean;
}

// === Game master notepad ===
// The entry shape is not declared here: all three notepads (personal, game,
// blog) are served by one API DTO and are declared once in
// shared/api/models/notepads. A local copy is how this one drifted into a
// notepadType union with members the server has never sent.

// === Room mutations ===

/** Payload for creating a post room (maps to backend CreateRoomRequest) */
export interface CreateRoomInput {
  title: string;
  type: RoomType;
  accessType: RoomAccessType;
  viewPrivateText?: boolean;
  viewDiceResults?: boolean;
  diceEnabled?: boolean;
}

/** Payload for creating a post pendency (whose turn it is to post) */
export interface PostPendencyInput {
  characterId: string;
}

/**
 * A single dice roll request submitted with a new post (server rolls it).
 * Maps to backend CreatePostDiceRoll (doc 4.2.2.13).
 */
export interface DiceRollInput {
  /** Number of sides (Y in XdY, e.g. 20 for a d20) */
  dice: number;
  /** Number of dice to roll (X in XdY); defaults to 1 server-side */
  count?: number;
  /** Flat modifier applied to the total */
  bonus?: number;
  /** Max explosions per die when it rolls its maximum value (0/undefined = no explode) */
  explosion?: number;
  /** Whether the result is visible to everyone ("показывать всем результаты") */
  public?: boolean;
  /** Optional comment shown next to the roll */
  comment?: string;
}

/**
 * Payload for creating a post (maps to backend CreatePostRequest).
 * CharacterId is omitted for master/assistant posts.
 */
export interface CreatePostInput {
  /** Character id; omit to post as master/assistant */
  characterId?: string;
  gameText: string;
  metagameText?: string;
  /** Dice rolls requested with the post (only when the room has dice enabled) */
  diceRolls?: DiceRollInput[];
}

/**
 * Payload for editing a post (maps to backend PATCH v1/posts/{id}, which binds
 * a partial Post DTO). Only the text fields are round-tripped; omitting the
 * character leaves it unchanged.
 */
export interface UpdatePostInput {
  gameText: string;
  metagameText?: string;
}

// === Game state-machine transitions (mirror the backend enums) ===

/**
 * Requested game status transition.
 * @see src/DM.Domain.Core/Statuses/ModuleStatusTransition.cs
 */
export enum GameStatusTransition {
  /** Draft -> Active */
  Start = "Start",
  /** Active -> Closed (frozen) */
  Freeze = "Freeze",
  /** Active -> Closed (finished) */
  Finish = "Finish",
  /** Active or frozen -> Closed */
  Close = "Close",
  /** Closed -> Active */
  Reopen = "Reopen",
}

/**
 * Requested premoderation transition. Exactly three, and they do not share an
 * actor: the first two are the moderation verdict (Mentor and above, legal from
 * any status), the third is the master asking for that verdict.
 * @see src/DM.Domain.Core/Statuses/ModuleStatusTransition.cs
 */
export enum GamePremoderationTransition {
  /** Any status -> Approved (verdict, clears the curator) */
  SetApproved = "SetApproved",
  /** Any status -> AwaitingEdits (verdict, records the acting mentor) */
  SetAwaitingEdits = "SetAwaitingEdits",
  /** AwaitingEdits -> AwaitingApproval (the master asks for a verdict) */
  SubmitForApproval = "SubmitForApproval",
}
