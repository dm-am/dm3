/**
 * @vitest-environment jsdom
 */

/**
 * The subscription row of the game menu in the left sidebar.
 *
 * A button of a sidebar answers a refusal with a plate, not with a line under
 * its row (UI_STANDARDS, "Всплывающие сообщения"): the menu is a column of
 * short rows, and a line put under one of them pushed every row below it
 * down. What is checked is that the refusal reaches the plate in the words of
 * the press, and that the menu gains no row for it.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  useGameDetailsStore,
  type Character,
  type Game,
} from "@/entities/game";
import { personalApi, useAuthStore } from "@/entities/user";
import type { ReceivedInvitation } from "@/shared/api/models/personal";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import GameJoinActions from "./GameJoinActions.vue";

vi.mock("vue-router", () => ({ useRouter: () => ({ push: vi.fn() }) }));

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((one) => one.message);

/** A refusal the server named nothing in: the plate says the press. */
const refused: GeneralError = {
  status: 404,
  title: "",
  type: "",
  traceId: "t",
};

function mountStrip() {
  const store = useGameDetailsStore();
  store.game = {
    id: "g-1",
    publicId: "abcde",
    title: "Хроники",
    participation: [],
  } as unknown as Game;
  return mount(GameJoinActions, { props: { variant: "strip" } });
}

describe("GameJoinActions in the sidebar", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  afterEach(() => {
    toast.toasts.value.forEach((one) => toast.dismiss(one.id));
    vi.restoreAllMocks();
  });

  it("answers a refused subscription with a plate and adds no row", async () => {
    const wrapper = mountStrip();
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
    const wrapper = mountStrip();
    vi.spyOn(useGameDetailsStore(), "subscribe").mockResolvedValue(null);

    await wrapper.get("button").trigger("click");
    await flushPromises();

    expect(saidOutLoud()).toEqual([]);
    wrapper.unmount();
  });
});

/**
 * The game's blacklist closes the application, so "Подать заявку" is not in
 * the menu of the viewer on it, recruiting or not, and an application of his
 * is not offered for editing. The sheet of a character in play or out of it
 * stays his: "Редактировать персонажа" leads to it as it does for anybody.
 */
describe("GameJoinActions on the blacklist", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  function mountMenu(blacklisted: boolean, status = "Retired") {
    useAuthStore().user = { id: "me" } as never;
    const store = useGameDetailsStore();
    store.game = {
      id: "g-1",
      publicId: "abcde",
      title: "Хроники",
      participation: [],
      recruitment: { isOpen: true },
      viewerBlacklisted: blacklisted,
    } as unknown as Game;
    store.characters = [
      {
        id: "c-1",
        isNpc: false,
        status,
        author: { id: "me", username: "me" },
      } as unknown as Character,
    ];
    return mount(GameJoinActions, {
      props: { variant: "strip" },
      global: { stubs: { RouterLink: { template: "<a><slot /></a>" } } },
    });
  }

  const rows = (wrapper: ReturnType<typeof mountMenu>) =>
    wrapper.findAll("li").map((li) => li.text().replace(/^-/, "").trim());

  it("takes Подать заявку away and leaves the sheet of a character", () => {
    expect(rows(mountMenu(true))).toEqual([
      "Подписаться",
      "Редактировать персонажа",
    ]);
  });

  it("does not offer an application of the viewer for editing", () => {
    expect(rows(mountMenu(true, "UnderReview"))).toEqual(["Подписаться"]);
  });

  it("offers both off the list", () => {
    expect(rows(mountMenu(false))).toEqual([
      "Подписаться",
      "Подать заявку",
      "Редактировать персонажа",
    ]);
  });
});

/**
 * A pending invitation to play opens the application of an active game whose
 * recruitment is shut, so the viewer invited to play finds "Подать заявку" in
 * the menu as anybody finds it in a recruiting game. The game does not carry
 * the invitation: the viewer's own list of invitations does.
 */
describe("GameJoinActions with recruitment shut", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
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
    game: Record<string, unknown> = {},
  ) {
    const asked = vi.spyOn(personalApi, "getMyInvitations").mockResolvedValue({
      data: { resources: invitations },
      error: null,
    } as never);
    useAuthStore().user = { id: "me" } as never;
    const store = useGameDetailsStore();
    store.game = {
      id: "g-1",
      publicId: "abcde",
      title: "Хроники",
      status: "Active",
      participation: [],
      recruitment: { isOpen: false },
      viewerBlacklisted: false,
      ...game,
    } as unknown as Game;
    const wrapper = mount(GameJoinActions, { props: { variant: "strip" } });
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
      viewerBlacklisted: true,
    });

    expect(rows).toEqual(["Подписаться"]);
    expect(asked).not.toHaveBeenCalled();
  });

  it("does not ask for a game that is not active", async () => {
    const { rows, asked } = await mountMenu([invitation()], {
      status: "Closed",
    });

    expect(rows).toEqual(["Подписаться"]);
    expect(asked).not.toHaveBeenCalled();
  });

  it("does not ask while the game recruits: the row is there anyway", async () => {
    const { rows, asked } = await mountMenu([], {
      recruitment: { isOpen: true },
    });

    expect(rows).toEqual(["Подписаться", "Подать заявку"]);
    expect(asked).not.toHaveBeenCalled();
  });
});
