import type { Id, Served } from "@/api/models";
import type { User } from "@/api/models/community";
import type { Character } from "@/api/models/gaming/characters";

export type RoomId = Id<string>;

export enum RoomType {
  Clean = "Clean",
  Hybrid = "Hybrid",
  Chat = "Chat",
}

export enum RoomAccessType {
  Open = "Open",
  Readable = "Readable",
  Closed = "Closed",
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
