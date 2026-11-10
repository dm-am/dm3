/**
 * @vitest-environment jsdom
 */

// A sidebar button answers a refusal with a plate: a line would push every row below it down.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import type { Router } from "vue-router";
import {
  GameStatus,
  useGameDetailsStore,
  type Character,
  type Game,
} from "@/entities/game";
import { createMockGame } from "@/entities/game/model/store.fixtures";
import { personalApi, useAuthStore } from "@/entities/user";
import type { ReceivedInvitation } from "@/shared/api/models/personal";
import { useToast } from "@/shared/lib/composables/useToast";
import {
  UserRole,
  type GeneralError,
  type UserRef,
  type Username,
} from "@/shared/api/models/common";
import { asServed } from "@/shared/api/models";
import { mountPage } from "@/shared/lib/testing/mountPage";
import GameJoinActions from "./GameJoinActions.vue";

const ROUTES: Parameters<typeof mountPage>[2]["routes"] = [
  ["game", "/game/:id"],
  ["game-character-create", "/game/:id/characters/create"],
  ["game-character-edit", "/game/:id/characters/:characterId/edit"],
];

// The public id is what the strip links by; recruitment is shut unless the case opens it.
const gameOf = (
  over: {
    status?: GameStatus;
    recruiting?: boolean;
    blacklisted?: boolean;
  } = {},
): Game => {
  const game = createMockGame("g-1", "Хроники");
  return {
    ...game,
    publicId: asServed("abcde"),
    status: over.status ?? game.status,
    recruitment: asServed({
      ...game.recruitment,
      isOpen: over.recruiting ?? false,
    }),
    viewerBlacklisted: asServed(over.blacklisted ?? false),
  };
};

const me: UserRef = {
  id: "me",
  username: "me" as Username,
  lastActivityUtc: "2024-01-01T00:00:00Z",
  role: UserRole.RegularUser,
  isNewbie: false,
};

const characterOf = (status: Character["status"]): Character => ({
  id: asServed("c-1"),
  status,
  author: asServed(me),
  name: "Лекс",
  isNpc: asServed(false),
  isDead: asServed(false),
  isPlayerLeft: asServed(false),
  isPlayerExiled: asServed(false),
  picture: asServed({}),
  totalPostsCount: asServed(0),
});

// The stores the strip reads are filled in before it mounts, so the page gets this pinia.
let pinia: Pinia;
let router: Router;
const freshPinia = () => {
  pinia = createPinia();
  setActivePinia(pinia);
};
const mountStripAt = async (address = "/game/abcde") => {
  const mounted = await mountPage(GameJoinActions, address, {
    routes: ROUTES,
    pinia,
  });
  router = mounted.router;
  return mounted;
};

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((one) => one.message);

const refused: GeneralError = {
  status: 404,
  title: "",
  type: "",
  traceId: "t",
};

async function mountStrip() {
  useGameDetailsStore().game = gameOf();
  return (await mountStripAt()).wrapper;
}

describe("GameJoinActions in the sidebar", () => {
  beforeEach(() => {
    freshPinia();
  });

  afterEach(() => {
    toast.toasts.value.forEach((one) => toast.dismiss(one.id));
    vi.restoreAllMocks();
  });

  it("answers a refused subscription with a plate and adds no row", async () => {
    const wrapper = await mountStrip();
    const rowsBefore = wrapper.findAll("li").length;
    vi.spyOn(useGameDetailsStore(), "subscribe").mockResolvedValue(refused);

    await wrapper.get("button").trigger("click");
    await flushPromises();

    expect(saidOutLoud()).toEqual(["Не удалось изменить подписку"]);
    expect(wrapper.findAll("li")).toHaveLength(rowsBefore);
    expect(wrapper.text()).not.toContain("Не удалось");
    wrapper.unmount();
  });

  it("says nothing when the subscription goes through", async () => {
    const wrapper = await mountStrip();
    vi.spyOn(useGameDetailsStore(), "subscribe").mockResolvedValue(null);

    await wrapper.get("button").trigger("click");
    await flushPromises();

    expect(saidOutLoud()).toEqual([]);
    wrapper.unmount();
  });
});

describe("GameJoinActions on the blacklist", () => {
  beforeEach(() => {
    freshPinia();
  });

  async function mountMenu(
    blacklisted: boolean,
    status: Character["status"] = "Retired",
  ) {
    useAuthStore().user = { id: "me" } as never;
    const store = useGameDetailsStore();
    store.game = gameOf({ recruiting: true, blacklisted });
    store.characters = [characterOf(status)];
    return (await mountStripAt()).wrapper;
  }

  const rows = (wrapper: VueWrapper) =>
    wrapper.findAll("li").map((li) => li.text().replace(/^-/, "").trim());

  it("takes Подать заявку away and leaves the sheet of a character", async () => {
    expect(rows(await mountMenu(true))).toEqual([
      "Подписаться",
      "Редактировать персонажа",
    ]);
  });

  it("does not offer an application of the viewer for editing", async () => {
    expect(rows(await mountMenu(true, "UnderReview"))).toEqual(["Подписаться"]);
  });

  it("leads the character's sheet to its editing by the public id of the game", async () => {
    const wrapper = await mountMenu(false);

    expect(wrapper.get("a").attributes("href")).toBe(
      "/game/abcde/characters/c-1/edit",
    );
  });

  it("takes Подать заявку to the form of a character in this game", async () => {
    const wrapper = await mountMenu(false);

    await wrapper
      .findAll("button")
      .find((one) => one.text() === "Подать заявку")!
      .trigger("click");
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe(
      "/game/abcde/characters/create",
    );
  });

  it("offers both off the list", async () => {
    expect(rows(await mountMenu(false))).toEqual([
      "Подписаться",
      "Подать заявку",
      "Редактировать персонажа",
    ]);
  });
});

describe("GameJoinActions with recruitment shut", () => {
  beforeEach(() => {
    freshPinia();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const invitation = (
    over: Partial<ReceivedInvitation> = {},
  ): ReceivedInvitation => ({
    id: "t-1",
    entityId: "g-1",
    entityType: "game",
    entityTitle: "Хроники",
    inviterUsername: "master",
    type: "player",
    createdUtc: "2026-10-01T10:00:00Z",
    ...over,
  });

  async function mountMenu(
    invitations: ReceivedInvitation[],
    game: Parameters<typeof gameOf>[0] = {},
  ) {
    const asked = vi.spyOn(personalApi, "getMyInvitations").mockResolvedValue({
      data: { resources: invitations },
      error: null,
    } as never);
    useAuthStore().user = { id: "me" } as never;
    const store = useGameDetailsStore();
    store.game = gameOf(game);
    const { wrapper } = await mountStripAt();
    await flushPromises();
    const rows = wrapper
      .findAll("li")
      .map((li) => li.text().replace(/^-/, "").trim());
    return { rows, asked };
  }

  it("offers Подать заявку to the viewer invited to play", async () => {
    const { rows } = await mountMenu([invitation()]);

    expect(rows).toEqual(["Подписаться", "Подать заявку"]);
  });

  it("does not offer it for an invitation to read or to another game", async () => {
    const { rows } = await mountMenu([
      invitation({ type: "reader" }),
      invitation({ entityId: "g-2" }),
    ]);

    expect(rows).toEqual(["Подписаться"]);
  });

  it("keeps it away from the viewer on the blacklist, without asking", async () => {
    const { rows, asked } = await mountMenu([invitation()], {
      blacklisted: true,
    });

    expect(rows).toEqual(["Подписаться"]);
    expect(asked).not.toHaveBeenCalled();
  });

  it("does not ask for a game that is not active", async () => {
    const { rows, asked } = await mountMenu([invitation()], {
      status: GameStatus.Closed,
    });

    expect(rows).toEqual(["Подписаться"]);
    expect(asked).not.toHaveBeenCalled();
  });

  it("does not ask while the game recruits: the row is there anyway", async () => {
    const { rows, asked } = await mountMenu([], {
      recruiting: true,
    });

    expect(rows).toEqual(["Подписаться", "Подать заявку"]);
    expect(asked).not.toHaveBeenCalled();
  });
});
