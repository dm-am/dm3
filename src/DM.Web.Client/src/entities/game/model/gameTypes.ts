import {
  type User,
  type UserRef,
  ClosedReason,
  type PremoderationStatus,
  ModuleStatus,
} from "@/shared/api/models/common";
import type { Served } from "@/shared/api/models";
import type {
  AttributeSpecification,
  GameAttributeSchema,
} from "@/shared/api/models/game/attributeSchemas";
import type {
  ActiveCharacterInfo,
  PlayerCharacterInfo,
  ApiCharacterStatus,
} from "./characterTypes";

export const GameStatus = ModuleStatus;
export type GameStatus = ModuleStatus;

/** The API's participation flags, not the domain's GameRole. */
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

export type Tag = {
  /** The short numeric id the filters use. */
  id: number;
  title: string;
  /** Rendered by the server for the hint surface. */
  description?: string;
  groupTitle: string;
  groupDescription?: string;
  gamesCount: number;
  sortOrder: number;
  groupSortOrder: number;
  /** Read from the server so the picker holds no copy; absent means no limit. */
  groupMaxTagsPerGame?: number | null;
};

/** Who may write in the discussion of a game; everybody may read it. */
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
  startedUtc?: string;
  isSubsequent: boolean;
}

export type GameId = Served<string>;

/** The game as `?projection=ref` serves it, for sidebars and menus. */
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
  participation: Served<GameParticipation[]>;
  subscribersCount: number;
  recruitment: Served<GameRecruitment>;
  unreadPostsCount: Served<number>;
  unreadCommentsCount: Served<number>;
  /** Reviews of the game itself. */
  gameReviewsCount: Served<number>;
  /** Reviews of the posts in the game. */
  postReviewsCount: Served<number>;
  /** The first twenty, for the tooltip. */
  subscriberUsernames?: string[];
  activeCharacters?: ActiveCharacterInfo[];
  /** Summed up by the server, so a list shows the marker without reading every room. */
  awaitsViewerTurn?: boolean;
  awaitedCharacterNames?: string[];
};

/** Each flag is asked by the code that refuses the write it stands for. */
interface GameViewer {
  canComment: boolean;
  canReview: boolean;
}

export interface Game extends GameRef {
  system: string;
  setting: string;
  closedUtc?: string;
  createdUtc: string;

  /** On the game page only. */
  fullAssistants?: Served<User[]>;
  pendingAssistant: Served<UserRef | null>;
  mentor: Served<UserRef | null>;
  /** A game that does not say is treated as needing nothing. */
  premoderationStatus?: PremoderationStatus;
  info: string;

  /** On the game page only; lists carry tagIds. */
  tags?: Tag[];
  tagIds: number[];
  privacySettings: GamePrivacySettings;
  /** The details always carry it; a list leaves it out. */
  schema?: GameAttributeSchema;

  // The game page only, down to playerCharacters.
  /** The authors of active characters. */
  players?: Served<UserRef[]>;
  /** The subscribers. */
  readers?: Served<UserRef[]>;
  /** The comment form goes by `viewer.canComment`, which counts the blacklist too. */
  viewerBlacklisted?: Served<boolean>;
  viewer?: Served<GameViewer>;
  totalPostsCount?: Served<number>;
  masterPostsCount?: Served<number>;
  lastMasterPostUtc?: Served<string | null>;
  /** Any room of the game rolls dice. */
  diceSupported?: Served<boolean>;

  /** Only on a list asked with the playerUsername filter. */
  playerCharacters?: PlayerCharacterInfo[];

  unreadCharactersCount: Served<number>;
}

interface CreateGamePrivacySettingsInput {
  viewPrivates: boolean;
  viewDice: boolean;
  viewPostStats: boolean;
  commentariesAccess: CommentariesAccessMode;
}

export interface CreateGameInput {
  title: string;
  system?: string;
  setting?: string;
  /** Raw BBCode. */
  info: string;
  /** In the making, out of sight of everyone without a role; false starts it. */
  draft?: boolean;
  /** The short identifiers /games/tags serves. */
  tags?: number[];
  /** Saved with the game; an empty set means characters without attributes. */
  attributeSpecifications?: AttributeSpecification[];
  assistantUsername?: string;
  privacySettings?: CreateGamePrivacySettingsInput;
}

interface UpdateGamePrivacySettingsInput {
  viewPrivates?: boolean;
  viewDice?: boolean;
  viewPostStats?: boolean;
  commentariesAccess?: CommentariesAccessMode;
}

interface UpdateGameRecruitmentInput {
  isOpen?: boolean;
  pcLimit?: number;
}

// Not Partial<Game>: the response carries fields the endpoint drops without a word.
export interface UpdateGameInput {
  title?: string;
  system?: string;
  setting?: string;
  info?: string;
  /** The whole set: left out keeps the tags, an empty list clears them. */
  tags?: number[];
  privacySettings?: UpdateGamePrivacySettingsInput;
  recruitment?: UpdateGameRecruitmentInput;
}

export type { Invitation } from "@/shared/api/models/game/invitations";

export interface GameUser {
  user: UserRef;
  role: "master" | "mentor" | "assistant" | "player" | "reader";
  joinedUtc: string;
  characterId?: string;
  characterName?: string;
  characterStatus?: ApiCharacterStatus;
}

export {
  AttributeSpecificationType,
  type AttributeValueSpecification,
  type AttributeSpecification,
  type GameAttributeSchema,
  type ReplaceGameAttributeSchemaRequest,
} from "@/shared/api/models/game/attributeSchemas";

// No id: the game may not exist yet, and the server hands out the specification ids on save.
export interface AttributeSchemaDraft {
  title: string;
  specifications: AttributeSpecification[];
}

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

/** The first two are the moderation verdict, the third the master asking for it. */
export enum GamePremoderationTransition {
  /** Any status -> Approved (verdict, clears the curator) */
  SetApproved = "SetApproved",
  /** Any status -> AwaitingEdits (verdict, records the acting mentor) */
  SetAwaitingEdits = "SetAwaitingEdits",
  /** AwaitingEdits -> AwaitingApproval (the master asks for a verdict) */
  SubmitForApproval = "SubmitForApproval",
}
