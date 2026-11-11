import type { UserRef } from "@/shared/api/models/common";
import type { Served } from "@/shared/api/models";
import type { Character } from "./characterTypes";
import type { GameRef } from "./gameTypes";

type RoomId = Served<string>;

export enum RoomType {
  /** Posts. */
  Default = "Default",
  /** Messages, paged by cursor. */
  Chat = "Chat",
}

export enum RoomAccessType {
  Open = "Open",
  /** Only the players of the characters let in. */
  Private = "Private",
}

/** Full, which also writes, is only for characters. */
export enum RoomAccessPolicy {
  ReadOnly = "ReadOnly",
  Full = "Full",
}

/** A character let in, or a reader, who has no character and only reads. */
export interface RoomAccess {
  id: string;
  policy?: RoomAccessPolicy;
  character?: Character | null;
  user?: UserRef | null;
}

/**
 * A grant as the server reads its body (RoomMapper.ToCreateRoomAccess): of the
 * character or the reader only what names them, and the policy.
 */
export type RoomAccessGrant = { policy: RoomAccessPolicy } & (
  { character: { id: string } } | { user: { username: string } }
);

/** Stays in the payload once answered, with fulfilledUtc, so every reader filters. */
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

interface RoomSettings {
  viewPrivateText: boolean;
  viewDiceResults: boolean;
  diceEnabled: boolean;
  /** The server drops the room for a viewer without access; only the form reads this. */
  hiddenWithoutAccess: boolean;
}

export type Room = {
  id: Served<RoomId>;
  roomNumber: Served<number>;
  title: string;
  access?: RoomAccessType;
  type?: RoomType;
  /** Missing reads as not archived. */
  isArchived?: boolean;
  /** False in the listing for a private room the viewer may not enter. */
  canView?: boolean;
  accesses?: RoomAccess[];
  pendencies?: PostPendency[];
  unreadPostsCount: number;
  /** Removed ones left out; zero on a room the viewer may not open. */
  totalPostsCount?: number;
  /** Who may write in a room the viewer may not open, in the order they joined. */
  characterNames?: string[];
  settings?: RoomSettings;
  /** Served whole by the post lists, so GameLink needs no request of its own. */
  game?: GameRef;
};

export interface CreateRoomInput {
  title: string;
  type: RoomType;
  accessType: RoomAccessType;
  viewPrivateText?: boolean;
  viewDiceResults?: boolean;
  diceEnabled?: boolean;
}

export interface PostPendencyInput {
  characterId: string;
}
