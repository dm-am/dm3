import {
  CommentariesAccessMode,
  GameStatus,
  type Game,
  type GameRef,
  type GameId,
  type GameRecruitment,
  type GamePrivacySettings,
} from "./gameTypes";
import type { Post } from "./postTypes";
import type { Character } from "./characterTypes";
import type { UserRef } from "@/shared/api/models/common";
import { vi } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import { asServed } from "@/shared/api/models";

const createMockUserRef = (id: string, username: string): UserRef =>
  ({
    id,
    username,
    lastActivityUtc: "2024-01-01T00:00:00Z",
  }) as UserRef;

const createMockRecruitment = (): GameRecruitment => ({
  isOpen: true,
  pcCount: 0,
  isSubsequent: false,
});

const createMockPrivacySettings = (): GamePrivacySettings => ({
  viewPrivates: true,
  viewDice: true,
  commentariesAccess: CommentariesAccessMode.Public,
});

export const createMockGameRef = (id: string, title: string): GameRef => ({
  id: asServed(id as GameId),
  publicId: asServed(
    id
      .slice(0, 5)
      .toLowerCase()
      .replace(/[^a-z]/g, "a"),
  ),
  title,
  status: GameStatus.Active,
  master: asServed(createMockUserRef("user-1", "master")),
  assistants: asServed([]),
  participation: asServed([]),
  subscribersCount: 0,
  recruitment: asServed(createMockRecruitment()),
  unreadPostsCount: asServed(0),
  unreadCommentsCount: asServed(0),
  gameReviewsCount: asServed(0),
  postReviewsCount: asServed(0),
});

export const createMockGame = (id: string, title: string): Game => ({
  ...createMockGameRef(id, title),
  system: "Test System",
  setting: "Test Setting",
  createdUtc: "2024-01-01T00:00:00Z",
  pendingAssistant: asServed(null),
  mentor: asServed(null),
  info: "",
  tagIds: [],
  privacySettings: createMockPrivacySettings(),
  unreadCharactersCount: asServed(0),
});

export const createMockPost = (id: string, gameText: string): Post => ({
  id: asServed(id),
  createdUtc: "2024-01-01T00:00:00Z",
  gameText,
});

/** An active player character as the roster reads it: no privacy and no attributes. */
export const createMockCharacter = (id: string, name: string): Character => ({
  id: asServed(id),
  status: "Active",
  isDead: asServed(false),
  isPlayerLeft: asServed(false),
  isPlayerExiled: asServed(false),
  name,
  picture: asServed({}),
  isNpc: asServed(false),
  totalPostsCount: asServed(0),
});

/** A store of its own for every case, and mocks with no calls left in them. */
export function freshPinia(): void {
  setActivePinia(createPinia());
  vi.clearAllMocks();
}
