import type { PagingQuery } from "@/api/models/common";
import type { User } from "@/api/models/community";
import type { AttributeSchema } from "@/api/models/game/attributes";
import type { Id, Served } from "@/api/models";

export enum GameStatus {
  Draft = "Draft",
  Active = "Active",
  Closed = "Closed",
}

export enum GameParticipation {
  None = "None",
  Reader = "Reader",
  Player = "Player",
  Moderator = "Moderator",
  PendingAssistant = "PendingAssistant",
  Authority = "Authority",
  Owner = "Owner",
}

export type TagId = Id<string>;
export type Tag = {
  id: Served<TagId>;
  title: Served<string>;
  groupTitle: Served<string>;
  gamesCount: Served<number>;
};

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

export type GameId = Id<string>;
export type Game = {
  id: Served<GameId>;
  title: string;
  system: string;
  setting: string;
  status: GameStatus;
  participation: Served<GameParticipation[]>;
  released: Served<string>;

  master: Served<User>;
  assistant: User | null;
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
};

export interface GamesQuery extends PagingQuery {
  statuses: GameStatus[];
}

export type InvitationType = "assistant" | "player" | "reader";

export interface Invitation {
  id: string;
  gameId: string;
  gameTitle: string;
  invitedUser: User;
  inviterLogin: string;
  type: InvitationType;
  createdUtc: string;
}
