// Game entity types
// Migrated from api/models/game/

import type { PagingQuery, User, UserRef } from "@/shared/api/models/common";
import type { Id, Served } from "@/shared/api/models";

// === Game Status & Roles ===

export enum GameStatus {
  Draft = "Draft",
  Active = "Active",
  Closed = "Closed",
}

export enum ClosedReason {
  None = "None",
  Finished = "Finished",
  Frozen = "Frozen",
}

export enum DraftVisibility {
  Private = "Private",
  Public = "Public",
}

export enum GameRole {
  None = "None",
  Reader = "Reader",
  Applicant = "Applicant",
  Player = "Player",
  Mentor = "Mentor",
  Assistant = "Assistant",
  Master = "Master",
}

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
  viewTemper: boolean;
  viewStory: boolean;
  viewSkills: boolean;
  viewInventory: boolean;
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
  /** Whether this is a subsequent recruitment (донабор) */
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
  participation: Served<GameRole[]>;
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

  unreadCharactersCount: Served<number>;

  // Used only at creation time
  copyBlacklist?: boolean;
}

export interface GamesQuery extends PagingQuery {
  statuses: GameStatus[];
}

// === Invitations ===

export type InvitationType = "assistant" | "player" | "reader";

export interface Invitation {
  id: string;
  gameId: string;
  gameTitle: string;
  invitedUser: UserRef;
  inviterUsername: string;
  type: InvitationType;
  createdUtc: string;
  expiresUtc?: string;
}

export interface GameUser {
  user: UserRef;
  role: "master" | "mentor" | "assistant" | "player" | "reader";
  joinedUtc: string;
  characterId?: string;
  characterName?: string;
  characterStatus?: string;
}

// === Attribute Schemas ===

export enum AttributeSchemaType {
  Public = "Public",
  Private = "Private",
}

export enum AttributeSpecificationType {
  Number = "Number",
  String = "String",
  List = "List",
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
  minValue: number | null | undefined;
  maxValue: number | null | undefined;
  maxLength: number | null | undefined;
  values: AttributeValueSpecification[] | null | undefined;
}

export interface AttributeSchema {
  id: string | null;
  title: string;
  author: UserRef | null;
  type: AttributeSchemaType;
  specifications: AttributeSpecification[];
}

// === Characters ===

export enum CharacterStatus {
  Registration = "Registration",
  Declined = "Declined",
  Active = "Active",
  Dead = "Dead",
  Left = "Left",
}

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
  value: string;
  modifier: Served<string>;
  inconsistent: Served<string>;
};

export type CharacterId = Id<string>;
export type Character = {
  id: Served<CharacterId>;
  author: Served<UserRef>;
  status: CharacterStatus;
  name: string;
  race: string;
  class: string;
  alignment: Alignment;
  pictureUrl: Served<string>;
  /** Character is NPC (controlled by game master) */
  isNpc: Served<boolean>;
  appearance: string;
  temper: string;
  story: string;
  skills: string;
  inventory: string;
  privacy: CharacterPrivacySettings;
  attributes: Served<CharacterAttribute[]>;
  totalPostsCount: Served<number>;
};

// === Rooms & Posts ===

export type RoomId = Id<string>;

export enum RoomType {
  Clean = "Clean",
  Hybrid = "Hybrid",
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

export type PostId = Id<string>;
export type Post = {
  id: Served<PostId>;
  room?: Room;
  character?: Character;
  author?: UserRef;
  /** Author's game role: DungeonMaster, Assistant, or null for player posts */
  authorGameRole?: "DungeonMaster" | "Assistant";
  createdUtc: string;
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
 * post review (оценка поста)
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
