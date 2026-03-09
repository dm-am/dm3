// Game entity types
// Migrated from api/models/game/

import type { PagingQuery, User } from "@/shared/api/models/common";
import type { Id, Served } from "@/shared/api/models";

// === Game Status & Roles ===

export enum GameStatus {
  Draft = "Draft",
  Active = "Active",
  Closed = "Closed",
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

export type TagId = Id<string>;
export type Tag = {
  id: Served<TagId>;
  title: Served<string>;
  groupTitle: Served<string>;
  gamesCount: Served<number>;
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
  playerLimit?: number;
  playerCount: number;
}

// === Game ===

export type GameId = Id<string>;
export type Game = {
  id: Served<GameId>;
  title: string;
  system: string;
  setting: string;
  status: GameStatus;
  roles: Served<GameRole[]>;
  released: Served<string>;

  master: Served<User>;
  assistants: Served<User[]>;
  pendingAssistant: Served<User | null>;
  mentor: Served<User | null>;
  notes: string;
  info: string;

  tags: Tag[];
  privacySettings: GamePrivacySettings;
  schema: AttributeSchema | null;

  // Sidebar data
  activeCharacterUserIds: Served<string[]>;
  readerUserIds: Served<string[]>;
  recruitment: Served<GameRecruitment>;

  unreadPostsCount: Served<number>;
  unreadCommentsCount: Served<number>;
  unreadCharactersCount: Served<number>;

  // Used only at creation time
  copyBlacklist?: boolean;
};

export interface GamesQuery extends PagingQuery {
  statuses: GameStatus[];
}

// === Invitations ===

export type InvitationType = "assistant" | "player" | "reader";

export interface Invitation {
  id: string;
  gameId: string;
  gameTitle: string;
  invitedUser: User;
  inviterUsername: string;
  type: InvitationType;
  createdUtc: string;
  expiresUtc?: string;
}

export interface GameUser {
  user: User;
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
  author: User | null;
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
  author: Served<User>;
  status: CharacterStatus;
  name: string;
  race: string;
  class: string;
  alignment: Alignment;
  pictureUrl: Served<string>;
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

export interface PendingPost {
  id: string;
  characterId: string;
  characterName: string;
  createdAt: string;
  awaitingUser: User;
}

export interface RoomSettings {
  viewPrivateText: boolean;
  viewDiceResults: boolean;
  diceEnabled: boolean;
}

export type Room = {
  id: Served<RoomId>;
  previousRoomId?: string;
  title: string;
  access?: RoomAccessType;
  type?: RoomType;
  claims?: RoomClaim[];
  pendings?: PendingPost[];
  unreadPostsCount: number;
  settings?: RoomSettings;
};

export interface DiceRoll {
  id: string;
  dice: number;
  result: number;
  bonus: number;
  comment?: string;
}

export interface PostBbText {
  value: string;
  html: string;
}

export type PostId = Id<string>;
export type Post = {
  id: Served<PostId>;
  room?: Room;
  character?: Character;
  author?: User;
  createdUtc: string;
  updatedUtc?: string;
  text: PostBbText;
  commentary?: PostBbText;
  masterMessage?: PostBbText;
  diceRolls?: DiceRoll[];
};

// === Featured Posts ===

export type FeaturedPostId = Id<string>;

export interface FeaturedPost {
  id: Served<FeaturedPostId>;
  textPreview: string;
  author: User;
  characterName?: string;
  createdUtc: string;
  gameId: string;
  gameTitle: string;
  roomId: string;
  roomTitle: string;
  rating: number;
  reviewCount: number;
}

export interface FeaturedPostsEnvelope {
  bestOfWeek?: FeaturedPost;
  lastWithPlus?: FeaturedPost;
}

export enum ReviewSign {
  Positive = "Positive",
  Neutral = "Neutral",
  Negative = "Negative",
}

export interface PostReview {
  id: string;
  authorId: string;
  author?: User;
  targetType: string;
  targetId?: string;
  text?: string;
  sign?: ReviewSign;
  createdUtc: string;
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
