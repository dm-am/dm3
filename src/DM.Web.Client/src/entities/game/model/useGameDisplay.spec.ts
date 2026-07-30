/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import { useGameDisplay } from "./useGameDisplay";
import type { GameRef, Room } from "./types";
import { GameStatus, RoomAccessType, RoomType } from "./types";
import type { Served } from "@/shared/api/models";
import type { UserRef } from "@/shared/api/models/common";

// Pinia must be active so `useGameDisplay` — which touches Pinia-backed
// state via `useAuthStore` — can resolve its dependencies. Re-created
// fresh between tests to isolate store state.
beforeEach(() => {
  setActivePinia(createPinia());
});

function asServed<T>(value: T): Served<T> {
  return value as Served<T>;
}

function makeUser(username: string, id: string): UserRef {
  return {
    id: asServed(id),
    username,
    lastActivityUtc: asServed("2026-01-01T00:00:00Z"),
    role: asServed("RegularUser"),
    isNewbie: false,
  } as unknown as UserRef;
}

function makeGame(overrides: Partial<GameRef> = {}): GameRef {
  const base: GameRef = {
    id: asServed("00000000-0000-0000-0000-000000000040"),
    publicId: asServed("gamea"),
    title: "Test Game",
    status: GameStatus.Active,
    master: asServed(
      makeUser("testuser", "00000000-0000-0000-0000-000000000010"),
    ),
    assistants: asServed([]),
    participation: asServed([]),
    subscribersCount: 1,
    recruitment: asServed({
      isOpen: true,
      pcCount: 2,
      pcLimit: undefined,
      startedUtc: undefined,
      isSubsequent: false,
    }),
    unreadPostsCount: asServed(0),
    unreadCommentsCount: asServed(0),
    gameReviewsCount: asServed(0),
    postReviewsCount: asServed(0),
    subscriberUsernames: ["inactiveuser1"],
    activeCharacters: [
      { name: "Арагорн Следопыт", ownerUsername: "testuser" },
      { name: "Горим Железный Кулак", ownerUsername: "seconduser" },
    ],
    ...overrides,
  };
  return base;
}

function makeRoom(overrides: Partial<Room> = {}): Room {
  const base: Room = {
    id: asServed("00000000-0000-0000-0000-000000000041"),
    roomNumber: asServed(1),
    title: "Main Room",
    unreadPostsCount: 0,
    ...overrides,
  };
  return base;
}

describe("useGameDisplay.buildTooltip — game tooltip (sidebar + post unified)", () => {
  it("produces the exact same tooltip string when the same GameRef is passed in, regardless of context (sidebar vs featured post)", () => {
    const { buildTooltip } = useGameDisplay();
    const game = makeGame();
    const expected = ["Мастер: testuser", "Персонажи: 2/∞", "Читатели: 1"].join(
      "\n",
    );
    expect(buildTooltip(game)).toBe(expected);
  });

  it("includes assistants when present", () => {
    const { buildTooltip } = useGameDisplay();
    const game = makeGame({
      assistants: asServed([
        makeUser("helper1", "00000000-0000-0000-0000-00000000001a"),
        makeUser("helper2", "00000000-0000-0000-0000-00000000001b"),
      ]),
    });
    expect(buildTooltip(game)).toContain("Ассистенты: helper1, helper2");
  });

  it("uses a finite PC limit in the Персонажи line when the game has one", () => {
    const { buildTooltip } = useGameDisplay();
    const game = makeGame({
      recruitment: asServed({
        isOpen: true,
        pcCount: 2,
        pcLimit: 5,
        startedUtc: undefined,
        isSubsequent: false,
      }),
    });
    expect(buildTooltip(game)).toContain("Персонажи: 2/5");
  });
});

describe("useGameDisplay.buildRoomTooltip — room tooltip for featured / Pulse posts", () => {
  it("for an open room with active characters, renders the exact user-requested format", () => {
    const { buildRoomTooltip } = useGameDisplay();
    const game = makeGame();
    const room = makeRoom();

    const expected = [
      "Персонажи:",
      "- Арагорн Следопыт (testuser)",
      "- Горим Железный Кулак (seconduser)",
      "",
      "Доступ: открытый",
    ].join("\n");

    expect(buildRoomTooltip(room, game)).toBe(expected);
  });

  it("still renders the tooltip when room.access is undefined (post.room from the rated endpoint does not carry it)", () => {
    const { buildRoomTooltip } = useGameDisplay();
    const game = makeGame();
    const room = makeRoom();
    const result = buildRoomTooltip(room, game);
    expect(result).toContain("Персонажи:");
    expect(result).toContain("- Арагорн Следопыт (testuser)");
    expect(result).toContain("Доступ: открытый");
  });

  it("explicit Open access and undefined access produce identical output", () => {
    const { buildRoomTooltip } = useGameDisplay();
    const game = makeGame();
    const openRoom = makeRoom({
      access: RoomAccessType.Open,
      type: RoomType.Chat,
    });
    const undefRoom = makeRoom();
    expect(buildRoomTooltip(openRoom, game)).toBe(
      buildRoomTooltip(undefRoom, game),
    );
  });

  it("lists characters in the same order as game.activeCharacters", () => {
    const { buildRoomTooltip } = useGameDisplay();
    const game = makeGame({
      activeCharacters: [
        { name: "Эльвира Чародейка", ownerUsername: "RatingNegative" },
        { name: "Арагорн Следопыт", ownerUsername: "testuser" },
      ],
    });
    const room = makeRoom();
    const result = buildRoomTooltip(room, game);
    const elviraIdx = result.indexOf("Эльвира");
    const aragornIdx = result.indexOf("Арагорн");
    expect(elviraIdx).toBeGreaterThan(-1);
    expect(aragornIdx).toBeGreaterThan(-1);
    expect(elviraIdx).toBeLessThan(aragornIdx);
  });

  it("returns an empty string for a private room the viewer cannot see — callers skip the Tooltip wrapper", () => {
    const { buildRoomTooltip } = useGameDisplay();
    const game = makeGame();
    const privateRoom = makeRoom({
      access: RoomAccessType.Private,
      accesses: [],
    });
    expect(buildRoomTooltip(privateRoom, game, undefined)).toBe("");
  });
});

describe("useGameDisplay.formatSlots — games table status column", () => {
  it("shows current/limit when a finite pcLimit is set", () => {
    const { formatSlots } = useGameDisplay();
    expect(formatSlots({ recruitment: { pcCount: 2, pcLimit: 5 } })).toBe(
      "[2/5]",
    );
  });

  it("shows the infinity symbol when pcLimit is unset", () => {
    const { formatSlots } = useGameDisplay();
    expect(formatSlots({ recruitment: { pcCount: 3, pcLimit: null } })).toBe(
      "[3/∞]",
    );
    expect(formatSlots({})).toBe("[0/∞]");
  });
});

describe("useGameDisplay.buildSlotsTooltip — games table status column tooltip", () => {
  it("lists active characters and free slot count when pcLimit is finite", () => {
    const { buildSlotsTooltip } = useGameDisplay();
    const result = buildSlotsTooltip({
      recruitment: { pcCount: 1, pcLimit: 3 },
      activeCharacters: [
        { name: "Арагорн Следопыт", ownerUsername: "testuser" },
      ],
    });
    expect(result).toBe(
      [
        "Персонажи:",
        "- Арагорн Следопыт (testuser)",
        "",
        "Свободных мест: 2",
      ].join("\n"),
    );
  });

  it('shows "Мест нет" when the game is full', () => {
    const { buildSlotsTooltip } = useGameDisplay();
    const result = buildSlotsTooltip({
      recruitment: { pcCount: 3, pcLimit: 3 },
      activeCharacters: [],
    });
    expect(result).toBe(["Нет персонажей", "", "Мест нет"].join("\n"));
  });

  it('shows "без ограничений" when pcLimit is unset', () => {
    const { buildSlotsTooltip } = useGameDisplay();
    const result = buildSlotsTooltip({});
    expect(result).toBe(
      ["Нет персонажей", "", "Мест: без ограничений"].join("\n"),
    );
  });
});

describe("useGameDisplay.buildAssistantTooltip — games table master column tooltip", () => {
  it('uses singular "Ассистент" for one assistant', () => {
    const { buildAssistantTooltip } = useGameDisplay();
    expect(buildAssistantTooltip([{ username: "helper1" }])).toBe(
      "Ассистент: helper1",
    );
  });

  it('uses plural "Ассистенты" for multiple assistants', () => {
    const { buildAssistantTooltip } = useGameDisplay();
    expect(
      buildAssistantTooltip([{ username: "helper1" }, { username: "helper2" }]),
    ).toBe("Ассистенты: helper1, helper2");
  });
});
