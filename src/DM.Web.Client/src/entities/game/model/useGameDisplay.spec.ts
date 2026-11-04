/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import { useGameDisplay } from "./useGameDisplay";
import type { Game, GameRef } from "./gameTypes";
import { GameStatus } from "./gameTypes";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useAuthStore } from "@/shared/stores/auth";
import { type UserRef, ClosedReason } from "@/shared/api/models/common";
import { asServed } from "@/shared/api/models";

// Pinia must be active so `useGameDisplay` — which touches Pinia-backed
// state via `useAuthStore` — can resolve its dependencies. Re-created
// fresh between tests to isolate store state.
beforeEach(() => {
  setActivePinia(createPinia());
});

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

/** A game with its three dates apart, so each line names the one it reads. */
function makeDatedGame(overrides: Partial<Game> = {}): Game {
  return {
    ...makeGame(),
    createdUtc: "2026-01-05T10:00:00Z",
    activatedUtc: "2026-02-10T11:30:00Z",
    closedUtc: "2026-03-15T12:45:00Z",
    ...overrides,
  } as Game;
}

const recruiting = (isOpen: boolean) =>
  asServed({
    isOpen,
    pcCount: 2,
    pcLimit: undefined,
    startedUtc: "2026-02-20T09:15:00Z",
    isSubsequent: false,
  });

describe("useGameDisplay status — the status column, its date and its tooltip", () => {
  it("names the status the way the filter does", () => {
    const { formatStatus } = useGameDisplay();
    expect(formatStatus(makeGame({ status: GameStatus.Draft }))).toBe(
      "Оформляется",
    );
    expect(formatStatus(makeGame({ recruitment: recruiting(true) }))).toBe(
      "Набор игроков",
    );
    expect(formatStatus(makeGame({ recruitment: recruiting(false) }))).toBe(
      "Идет игра",
    );
    const closed = (closedReason: ClosedReason) =>
      makeGame({ status: GameStatus.Closed, closedReason });
    expect(formatStatus(closed(ClosedReason.Finished))).toBe("Завершена");
    expect(formatStatus(closed(ClosedReason.Frozen))).toBe("Заморожена");
    expect(formatStatus(closed(ClosedReason.None))).toBe("Закрыта");
  });

  it("lists in the tooltip every date the game has passed", () => {
    const { buildStatusTooltip } = useGameDisplay();
    const created = `Создание игры: ${formatDateFull("2026-01-05T10:00:00Z")}`;
    const started = `Начало игры: ${formatDateFull("2026-02-10T11:30:00Z")}`;

    expect(
      buildStatusTooltip(makeDatedGame({ status: GameStatus.Draft })),
    ).toBe(created);
    expect(
      buildStatusTooltip(makeDatedGame({ recruitment: recruiting(true) })),
    ).toBe(
      [
        created,
        started,
        `Начало последнего набора: ${formatDateFull("2026-02-20T09:15:00Z")}`,
      ].join("\n"),
    );
    expect(
      buildStatusTooltip(makeDatedGame({ status: GameStatus.Closed })),
    ).toBe(
      [
        created,
        started,
        `Закрытие игры: ${formatDateFull("2026-03-15T12:45:00Z")}`,
      ].join("\n"),
    );
  });
});

describe("useGameDisplay unread — the counters of a game and their tooltips", () => {
  it("counts a missing counter as none", () => {
    const { getUnreadPosts, getUnreadComments, hasUnread } = useGameDisplay();
    const silent = makeGame({
      unreadPostsCount: undefined,
      unreadCommentsCount: undefined,
    });
    expect(getUnreadPosts(silent)).toBe(0);
    expect(getUnreadComments(silent)).toBe(0);
    expect(hasUnread(silent)).toBe(false);
  });

  it("has unread content when either counter is above zero", () => {
    const { getUnreadPosts, getUnreadComments, hasUnread } = useGameDisplay();
    const posts = makeGame({ unreadPostsCount: asServed(3) });
    const comments = makeGame({ unreadCommentsCount: asServed(4) });
    expect(getUnreadPosts(posts)).toBe(3);
    expect(getUnreadComments(comments)).toBe(4);
    expect(hasUnread(posts)).toBe(true);
    expect(hasUnread(comments)).toBe(true);
  });

  it("calls the counters totals for a guest and unread for a signed-in reader", () => {
    const { formatUnreadPostsTooltip, formatUnreadCommentsTooltip } =
      useGameDisplay();
    expect(formatUnreadPostsTooltip(3)).toBe("Постов: 3");
    expect(formatUnreadCommentsTooltip(4)).toBe("Комментариев: 4");

    useAuthStore().user = { username: "reader" } as never;
    expect(formatUnreadPostsTooltip(3)).toBe("Непрочитанных постов: 3");
    expect(formatUnreadCommentsTooltip(4)).toBe(
      "Непрочитанных комментариев: 4",
    );
  });
});

describe("useGameDisplay.isNewHighlight — the green of a games list", () => {
  const startedAgo = (ms: number) => new Date(Date.now() - ms).toISOString();

  it("marks a game started a minute ago while it is active", () => {
    const { isNewHighlight } = useGameDisplay();
    expect(
      isNewHighlight({
        activatedUtc: startedAgo(60_000),
        status: GameStatus.Active,
      }),
    ).toBe(true);
  });

  it("never marks a closed game, however fresh its start", () => {
    const { isNewHighlight } = useGameDisplay();
    expect(
      isNewHighlight({
        activatedUtc: startedAgo(60_000),
        status: GameStatus.Closed,
      }),
    ).toBe(false);
  });

  it("leaves an active game started over a week ago unmarked", () => {
    const { isNewHighlight } = useGameDisplay();
    expect(
      isNewHighlight({
        activatedUtc: startedAgo(8 * 24 * 60 * 60 * 1000),
        status: GameStatus.Active,
      }),
    ).toBe(false);
  });
});
