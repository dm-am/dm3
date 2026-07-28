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
  /** Tag description (may contain [tipimg:] markup for inline image tooltips) */
  description?: string;
  groupTitle: string;
  /** Tag group description */
  groupDescription?: string;
  gamesCount: number;
  /** Tag sort order within its group */
  sortOrder: number;
  /** Tag group sort order */
  groupSortOrder: number;
};

// === Game Settings ===

export enum CommentariesAccessMode {
  Public = "Public",
  Readonly = "Readonly",
  Private = "Private",
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
  master: Served<UserRef>;
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
  draftVisibility?: DraftVisibility;
  closedUtc?: string;
  createdUtc: string;

  /** Full assistant details (only on game detail page) */
  fullAssistants?: Served<User[]>;
  pendingAssistant: Served<UserRef | null>;
  mentor: Served<UserRef | null>;
  notes: string;
  info: string;

  /** Full tags (only for single game details, null for lists) */
  tags?: Tag[];
  /** Tag IDs only (for lists - use cached /games/tags for descriptions) */
  tagIds: number[];
  privacySettings: GamePrivacySettings;
  schema: AttributeSchema | null;
  /** Attribute schema identifier */
  schemaId?: string;

  /** Unique players (authors of active characters) - for game details page */
  players?: Served<UserRef[]>;

  /**
   * Game readers ("Читатели") - the users subscribed to the game. A reader is a
   * game subscriber in the domain, so this mirrors the subscriber set. Present
   * only on the game details response.
   */
  readers?: Served<UserRef[]>;

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

  // Used only at creation time
  copyBlacklist?: boolean;
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
  /** Create the game as a draft (not visible to others) */
  draft?: boolean;
  /**
   * Game tag identifiers — backend Guids, NOT the numeric short ids served
   * by /games/tags. The public tag list does not expose the Guids, so this
   * field currently cannot be populated from TagSelector output.
   */
  tags?: string[];
  /** Attribute schema identifier */
  schemaId?: string;
  /** Assistant username */
  assistantUsername?: string;
  privacySettings?: CreateGamePrivacySettingsInput;
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

/**
 * Schema access type
 * @see src/DM.Domain.Core/Enums/SchemaType.cs
 */
export enum AttributeSchemaType {
  Public = "Public",
  Private = "Private",
}

/**
 * Specification constraints type
 * @see src/DM.Domain.Core/Enums/AttributeSpecificationType.cs
 */
export enum AttributeSpecificationType {
  Text = "Text",
  Number = "Number",
  TextList = "TextList",
  NumberList = "NumberList",
  TextNumberList = "TextNumberList",
  BbCode = "BbCode",
}

export interface AttributeValueSpecification {
  value: string;
  modifier: number | null;
}

export interface AttributeSpecification {
  id: string;
  title: string;
  required: boolean;
  type: AttributeSpecificationType;
  /** Order index within the schema */
  order: number;
  /** Show on game main page as descriptor (at most one per schema) */
  isDescriptor: boolean;
  /** Hide from other players (only GM and owner can see) */
  isHidden: boolean;
  /** Maximal length - only for Text/Number/BbCode types */
  maxLength?: number | null;
  /** Possible values - only for TextList/NumberList/TextNumberList types */
  values?: AttributeValueSpecification[] | null;
}

export interface AttributeSchema {
  id: string | null;
  title: string;
  author: UserRef | null;
  type: AttributeSchemaType;
  specifications: AttributeSpecification[];
}

// === Characters ===

export enum Alignment {
  LawfulGood = "LawfulGood",
  NeutralGood = "NeutralGood",
  ChaoticGood = "ChaoticGood",
  LawfulNeutral = "LawfulNeutral",
  TrueNeutral = "TrueNeutral",
  ChaoticNeutral = "ChaoticNeutral",
  LawfulEvil = "LawfulEvil",
  NeutralEvil = "NeutralEvil",
  ChaoticEvil = "ChaoticEvil",
}

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
  privacy: CharacterPrivacySettings;
  attributes: Served<CharacterAttribute[]>;
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

export interface RoomClaim {
  character: Character;
  claimedAt: string;
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

export interface PendingPost {
  id: string;
  characterId: string;
  characterName: string;
  createdUtc: string;
  awaitingUser: UserRef;
}

export interface RoomSettings {
  viewPrivateText: boolean;
  viewDiceResults: boolean;
  diceEnabled: boolean;
}

export type Room = {
  id: Served<RoomId>;
  roomNumber: Served<number>;
  previousRoomId?: string;
  title: string;
  access?: RoomAccessType;
  type?: RoomType;
  /**
   * Room is archived (hidden from the active rooms list, kept for history).
   * Absent on older payloads — treat missing as not archived.
   */
  isArchived?: boolean;
  claims?: RoomClaim[];
  /** Explicit per-room access grants (characters and/or readers) */
  accesses?: RoomAccess[];
  pendings?: PendingPost[];
  unreadPostsCount: number;
  settings?: RoomSettings;
  /**
   * Parent game reference for this room. Post-listing endpoints
   * (rated posts, pulse) populate this with a full sidebar-tier
   * GameRef — master, assistants, active characters, recruitment,
   * counts — so downstream GameLink / RoomLink components can
   * render tooltips without a second network round-trip per post.
   * Mirrors the sidebar's data-flow (ActiveGames, RecruitingGames,
   * OwnedGames all pass full GameRef objects straight into GameLink).
   */
  game?: GameRef;
};

export interface DiceRoll {
  id: string;
  dice: number;
  result: number;
  bonus: number;
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
  editor?: UserRef;
}

export type PostId = Id<string>;
export type Post = {
  id: Served<PostId>;
  room?: Room;
  character?: Character;
  author?: UserRef;
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
};

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
 * - Likes support (ONLY for PostReviews)
 * - One review per post per user
 */
export interface PostReview {
  id: string;
  postId: string;
  gameId: string;
  author: UserRef;
  postAuthor: UserRef;
  text: string;
  sign: ReviewSign;
  createdUtc: string;
  modifiedUtc?: string;
  likes: UserRef[];
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

// === Chat rooms (message-based rooms for OOC player communication) ===

/** Access grant on a chat room (character or reader) */
export interface ChatRoomAccess {
  id: string;
  user?: UserRef | null;
  character?: Character | null;
}

/**
 * Chat room in a game. Unlike post rooms, its content is a message stream
 * paginated with a cursor (see gameApi.getChatMessages).
 */
export interface ChatRoom {
  id: string;
  /** Linked chat identifier used for the message stream */
  chatId?: string | null;
  gameId: string;
  title: string;
  /** Display order number */
  orderNumber: number;
  /** Number of unread messages for the current user */
  unreadCount: number;
  accesses: ChatRoomAccess[];
}

/** Payload for creating a chat room */
export interface CreateChatRoomInput {
  title: string;
}

/** Payload for updating a chat room */
export interface UpdateChatRoomInput {
  title?: string;
}

// === Game master notepad ===

/**
 * Notepad type discriminator (mirrors backend NotepadType). Game master
 * notepad entries are always of the game-master kind.
 */
export type NotepadType = "User" | "GameMaster" | "Character" | "Blog";

/** A single game-master notepad entry */
export interface NotepadEntry {
  id: string;
  notepadType: NotepadType;
  containerId: string;
  ownerId?: string | null;
  categoryId?: string | null;
  title: string;
  content: string;
  sortOrder: number;
  createdUtc: string;
  modifiedUtc?: string | null;
}

/** Payload for creating a notepad entry */
export interface CreateNotepadEntryInput {
  categoryId?: string | null;
  title: string;
  content: string;
}

/** Payload for updating a notepad entry */
export interface UpdateNotepadEntryInput {
  categoryId?: string | null;
  title: string;
  content: string;
  sortOrder?: number | null;
}

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
 * @see src/DM.Domain.Game/Features/Games/GameStatusTransition.cs
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
 * Requested premoderation transition (mentor action).
 * @see src/DM.Domain.Game/Features/Games/GameStatusTransition.cs
 */
export enum GamePremoderationTransition {
  /** AwaitingEdits -> AwaitingApproval (take into premoderation) */
  SendToPremoderation = "SendToPremoderation",
  /** AwaitingApproval -> Approved (release) */
  RemoveFromPremoderation = "RemoveFromPremoderation",
}
