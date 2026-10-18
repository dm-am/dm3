/**
 * @vitest-environment jsdom
 */

/**
 * Turn tracking is the only mutation the room page owns, and both ends of it
 * used to swallow the refusal: the button re-enabled, the list stayed as it
 * was, and nothing said why — so the master pressed it again.
 *
 * These pin the outcome every neighbouring master screen already gives
 * (RoomsSection, RolesSection, BlacklistSection): one toast through
 * notifyFailure, and a refresh of the room only when the server agreed.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises, config } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { GameParticipation, useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/shared/stores";
import GameRoom from "./GameRoom.vue";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";

const {
  mockGetRooms,
  mockGetPosts,
  mockGetCharacters,
  mockMarkRoomAsRead,
  mockCreatePendency,
  mockDeletePendency,
  mockGetFirstUnreadRoomPost,
  mockCreatePost,
  mockReplace,
} = vi.hoisted(() => ({
  mockGetRooms: vi.fn(),
  mockGetPosts: vi.fn(),
  mockGetCharacters: vi.fn(),
  mockMarkRoomAsRead: vi.fn(),
  mockCreatePendency: vi.fn(),
  mockDeletePendency: vi.fn(),
  mockGetFirstUnreadRoomPost: vi.fn(),
  mockCreatePost: vi.fn(),
  mockReplace: vi.fn(),
}));

vi.mock("@/entities/game/api/gameApi", () => ({
  default: {
    getRooms: mockGetRooms,
    getPosts: mockGetPosts,
    getCharacters: mockGetCharacters,
    markRoomAsRead: mockMarkRoomAsRead,
    createPendency: mockCreatePendency,
    deletePendency: mockDeletePendency,
    getFirstUnreadRoomPost: mockGetFirstUnreadRoomPost,
    createPost: mockCreatePost,
  },
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({
    params: { id: "g-1", num: "1" },
    query: {},
    hash: "",
    path: "/game/g-1/rooms/1",
  }),
  useRouter: () => ({ push: vi.fn(), replace: mockReplace }),
  RouterLink: { template: "<a><slot /></a>" },
}));

/** A status the response interceptor stays quiet about, so the page must speak. */
const refusal = (status: number): GeneralError => ({
  type: "",
  title: "",
  status,
  traceId: "trace",
});

const room = {
  id: "r-1",
  roomNumber: 1,
  title: "Таверна",
  unreadPostsCount: 0,
  settings: { diceEnabled: false },
  pendencies: [
    {
      id: "p-1",
      roomId: "r-1",
      characterId: "c-1",
      characterName: "Гоблин",
      createdUtc: "2026-07-01T10:00:00Z",
      waitingFor: { username: "player" },
    },
  ],
  // The character is let into the room: the one a master may wait for.
  accesses: [{ id: "a-1", policy: "Full", character: { id: "c-1" } }],
};

const character = {
  id: "c-1",
  name: "Гоблин",
  status: "Active",
  isNpc: false,
  author: { username: "player" },
};

const messages = () => useToast().toasts.value.map((t) => t.message);

async function mountRoom() {
  const pinia = createPinia();
  setActivePinia(pinia);
  // The turn controls belong to the master, in the form of a post he writes
  // signed in; participation is what decides it.
  useAuthStore().updateUser({ username: "master" } as never);
  useGameDetailsStore().game = {
    id: "g-1",
    participation: [GameParticipation.Owner],
  } as never;

  const wrapper = mount(GameRoom, {
    shallow: true,
    // The refusal line is drawn for real: the assertions read its words.
    global: {
      plugins: [pinia],
      stubs: { RouterLink: true, ActionResult: false },
    },
  });
  await flushPromises();
  return wrapper;
}

describe("GameRoom turn tracking", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));

    vi.clearAllMocks();
    mockGetRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    mockGetPosts.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockGetCharacters.mockResolvedValue({
      data: { resources: [character], paging: null },
      error: null,
    });
    mockMarkRoomAsRead.mockResolvedValue({ data: null, error: null });
    mockCreatePendency.mockResolvedValue({ data: null, error: null });
    mockDeletePendency.mockResolvedValue({ data: null, error: null });
    // A signed-in reader asks where the room opens; the first page will do.
    mockGetFirstUnreadRoomPost.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "" },
    });
  });

  it("says why the character could not be added to the queue", async () => {
    mockCreatePendency.mockResolvedValue({ data: null, error: refusal(409) });
    const wrapper = await mountRoom();

    await wrapper.find(".pending-add-btn").trigger("click");
    await flushPromises();

    // Under the block of turns, where the press was; no plate.
    expect(wrapper.get(".turns .action-result").text()).toBe(
      "Не удалось добавить ожидание хода",
    );
    expect(messages()).toEqual([]);
  });

  it("says why the awaited turn could not be dropped", async () => {
    mockDeletePendency.mockResolvedValue({ data: null, error: refusal(404) });
    const wrapper = await mountRoom();

    await wrapper.find(".pending-dismiss").trigger("click");
    await flushPromises();

    expect(wrapper.get(".turns .action-result").text()).toBe(
      "Не удалось снять ожидание хода",
    );
    expect(messages()).toEqual([]);
  });

  it("refreshes the room and stays quiet when the server agreed", async () => {
    const wrapper = await mountRoom();
    const before = mockGetRooms.mock.calls.length;

    await wrapper.find(".pending-add-btn").trigger("click");
    await flushPromises();

    expect(mockGetRooms.mock.calls.length).toBe(before + 1);
    expect(messages()).toEqual([]);
  });

  it("does not refresh the room over a refused pendency", async () => {
    mockCreatePendency.mockResolvedValue({ data: null, error: refusal(409) });
    const wrapper = await mountRoom();
    const before = mockGetRooms.mock.calls.length;

    await wrapper.find(".pending-add-btn").trigger("click");
    await flushPromises();

    expect(mockGetRooms.mock.calls.length).toBe(before);
  });
});

/**
 * "Ждать хода" offers the characters the server takes and no others. The
 * server reads the one awaited off the room's accesses: an NPC has no player
 * and is refused, a character without an access is not in the room and is
 * refused, and so is one out of play. A choice that ends in a refusal is not
 * offered, and a room with nobody to wait for has no control to add one.
 */
describe("GameRoom turn tracking offers", () => {
  const npc = {
    id: "c-2",
    name: "Трактирщик",
    status: "Active",
    isNpc: true,
    author: null,
  };
  const outsider = {
    id: "c-3",
    name: "Странник",
    status: "Active",
    isNpc: false,
    author: { username: "other" },
  };
  const retired = {
    id: "c-4",
    name: "Ветеран",
    status: "Retired",
    isNpc: false,
    author: { username: "old" },
  };
  const admitted = (...ids: string[]) =>
    ids.map((id, i) => ({ id: `a-${i}`, policy: "Full", character: { id } }));

  async function mountWith(accesses: unknown[]) {
    mockGetRooms.mockResolvedValue({
      data: { resources: [{ ...room, accesses }], paging: null },
      error: null,
    });
    mockGetCharacters.mockResolvedValue({
      data: {
        resources: [character, npc, outsider, retired],
        paging: null,
      },
      error: null,
    });
    return mountRoom();
  }

  const offered = (wrapper: Awaited<ReturnType<typeof mountRoom>>) =>
    (
      wrapper
        .findAllComponents({ name: "Select" })
        .find((select) => select.classes("pending-select"))
        ?.props("options") as { label: string }[] | undefined
    )?.map((option) => option.label);

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPosts.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockMarkRoomAsRead.mockResolvedValue({ data: null, error: null });
    mockGetFirstUnreadRoomPost.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "" },
    });
  });

  it("offers only a character in play with a player and an access to the room", async () => {
    const wrapper = await mountWith(admitted("c-1", "c-2", "c-4"));

    expect(offered(wrapper)).toEqual(["Гоблин"]);
  });

  it("has no control to add a turn where nobody can be waited for", async () => {
    const wrapper = await mountWith([]);

    expect(wrapper.find(".pending-add").exists()).toBe(false);
    // The awaited turns already set stay, with their crosses.
    expect(wrapper.find(".pending-dismiss").exists()).toBe(true);
  });
});

// The metagame text is saved on the comment surface, which refuses a hidden
// block. The composer gives its editor the post's buttons and preview and the
// comment surface's markup check, so the refusal is named before the send.
describe("GameRoom composer", () => {
  it("checks the metagame text as the surface it is saved on", async () => {
    mockGetRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    mockGetPosts.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockGetCharacters.mockResolvedValue({
      data: { resources: [character], paging: null },
      error: null,
    });
    mockMarkRoomAsRead.mockResolvedValue({ data: null, error: null });
    // A signed-in reader asks where the room opens; the first page will do.
    mockGetFirstUnreadRoomPost.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "" },
    });
    const pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().updateUser({ username: "master" } as never);
    useGameDetailsStore().game = {
      id: "g-1",
      participation: [GameParticipation.Owner],
    } as never;

    const wrapper = mount(GameRoom, {
      shallow: true,
      // The refusal line is drawn for real: the assertions read its words.
      global: {
        plugins: [pinia],
        stubs: { RouterLink: true, ActionResult: false },
      },
    });
    await flushPromises();

    expect(
      wrapper
        .findAllComponents({ name: "BBCodeEditor" })
        .map((editor) => [
          editor.props("context"),
          editor.props("checkContext"),
        ]),
    ).toEqual([
      ["post", undefined],
      ["post", "common"],
    ]);
  });

  it("tells a refused post in the line over the button, and in no plate", async () => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
    mockGetRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    mockGetPosts.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockGetCharacters.mockResolvedValue({
      data: { resources: [character], paging: null },
      error: null,
    });
    mockMarkRoomAsRead.mockResolvedValue({ data: null, error: null });
    mockGetFirstUnreadRoomPost.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "" },
    });
    mockCreatePost.mockResolvedValue({ data: null, error: refusal(400) });
    const pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().updateUser({ username: "master" } as never);
    useGameDetailsStore().game = {
      id: "g-1",
      participation: [GameParticipation.Owner],
    } as never;

    const wrapper = mount(GameRoom, {
      shallow: true,
      // The refusal line is drawn for real: the assertions read its words.
      global: {
        plugins: [pinia],
        stubs: { RouterLink: true, ActionResult: false },
      },
    });
    await flushPromises();

    await wrapper
      .findAllComponents({ name: "BBCodeEditor" })[0]
      .vm.$emit("update:modelValue", "Ход");
    await wrapper.find(".composer-submit").trigger("click");
    await flushPromises();

    expect(mockCreatePost).toHaveBeenCalled();
    expect(wrapper.get(".composer .action-result").text()).toBe(
      "Не удалось отправить пост",
    );
    expect(messages()).toEqual([]);
  });
});

/**
 * The game's blacklist closes the discussion and the application, not the
 * rooms: who writes here is the roster's and the room's to say. The composer
 * used to give way to a sentence about the list for a seat the list held.
 */
describe("GameRoom composer on the game's blacklist", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPosts.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockMarkRoomAsRead.mockResolvedValue({ data: null, error: null });
    mockGetFirstUnreadRoomPost.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "" },
    });
  });

  /** The room of a game for the player, with the list saying what it says. */
  async function mountFor(opts: {
    blacklisted: boolean;
    characterStatus: string;
  }) {
    // The room lets the character write: the list is all that is asked here.
    mockGetRooms.mockResolvedValue({
      data: {
        resources: [{ ...room, pendencies: [] }],
        paging: null,
      },
      error: null,
    });
    mockGetCharacters.mockResolvedValue({
      data: {
        resources: [{ ...character, status: opts.characterStatus }],
        paging: null,
      },
      error: null,
    });
    const pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().updateUser({ username: "player" } as never);
    useGameDetailsStore().game = {
      id: "g-1",
      participation: [GameParticipation.Reader],
      viewerBlacklisted: opts.blacklisted,
    } as never;

    const wrapper = mount(GameRoom, {
      shallow: true,
      global: {
        plugins: [pinia],
        stubs: { RouterLink: true, SecondaryText: false },
      },
    });
    await flushPromises();
    return wrapper;
  }

  it("offers a player on the list the composer as anybody", async () => {
    const wrapper = await mountFor({
      blacklisted: true,
      characterStatus: "Active",
    });

    expect(wrapper.find(".composer").exists()).toBe(true);
    expect(wrapper.text()).not.toContain("черном списке");
  });

  it("offers no composer to a character out of play, list or not", async () => {
    const wrapper = await mountFor({
      blacklisted: false,
      characterStatus: "Retired",
    });

    expect(wrapper.find(".composer").exists()).toBe(false);
  });
});

/**
 * The composer offers "От лица" only the characters the room lets write: an
 * access of their own with "Может писать". The server asks exactly that of a
 * post as a character, in an open room too (RoomIntentionResolver.CreatePost),
 * and a player with no such character typed a post and was refused after
 * "Отправить". The master and the assistants write without a character in any
 * room of theirs.
 */
describe("GameRoom composer by the room's accesses", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPosts.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockMarkRoomAsRead.mockResolvedValue({ data: null, error: null });
    mockGetFirstUnreadRoomPost.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "" },
    });
  });

  const npc = {
    id: "c-npc",
    name: "Трактирщик",
    status: "Active",
    isNpc: true,
    author: null,
  };

  async function mountAs(
    username: string,
    participation: GameParticipation[],
    accesses: unknown[],
  ) {
    mockGetRooms.mockResolvedValue({
      data: {
        resources: [{ ...room, pendencies: [], accesses }],
        paging: null,
      },
      error: null,
    });
    mockGetCharacters.mockResolvedValue({
      data: { resources: [character, npc], paging: null },
      error: null,
    });
    const pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().updateUser({ username } as never);
    useGameDetailsStore().game = { id: "g-1", participation } as never;

    const wrapper = mount(GameRoom, {
      shallow: true,
      global: { plugins: [pinia], stubs: { RouterLink: true } },
    });
    await flushPromises();
    return wrapper;
  }

  const full = (id: string) => ({
    id: "a-" + id,
    policy: "Full",
    character: { id },
  });
  const readOnly = (id: string) => ({
    id: "a-" + id,
    policy: "ReadOnly",
    character: { id },
  });
  const offered = (wrapper: Awaited<ReturnType<typeof mountAs>>) =>
    wrapper.findAllComponents({ name: "BBCodeEditor" }).length > 0;
  const postAs = (wrapper: Awaited<ReturnType<typeof mountAs>>) =>
    (
      wrapper
        .findAllComponents({ name: "Select" })
        .find((one) => one.attributes("id") === "composer-post-as")
        ?.props("options") as { label: string }[] | undefined
    )?.map((option) => option.label) ?? [];

  it("offers the composer to a player the room lets write", async () => {
    const wrapper = await mountAs(
      "player",
      [GameParticipation.Player],
      [full("c-1")],
    );

    expect(offered(wrapper)).toBe(true);
  });

  // An open room with no access of the character: open to read, not to write.
  it("offers no composer to a player whose character the room does not let in", async () => {
    const wrapper = await mountAs("player", [GameParticipation.Player], []);

    expect(offered(wrapper)).toBe(false);
    expect(wrapper.find(".composer").exists()).toBe(false);
  });

  it("offers no composer to a player whose character may only read", async () => {
    const wrapper = await mountAs(
      "player",
      [GameParticipation.Player],
      [readOnly("c-1")],
    );

    expect(offered(wrapper)).toBe(false);
  });

  it("offers the master the NPCs the room lets write, and the master's own voice", async () => {
    const withNpc = await mountAs(
      "master",
      [GameParticipation.Owner],
      [full("c-npc")],
    );
    expect(postAs(withNpc)).toEqual(["Трактирщик (NPC)", "От лица мастера"]);

    const without = await mountAs(
      "master",
      [GameParticipation.Owner],
      [readOnly("c-npc")],
    );
    expect(offered(without)).toBe(true);
    expect(postAs(without)).toEqual([]);
  });
});

/**
 * The two lists of the room that name a character: "От лица:" over the post
 * and the one beside "Ждать хода". Both were 120 wide at every width, and a
 * name read "Таинственны"; the list beside "Ждать хода" was made as wide as
 * its longest name, never wider than the column, with the button under it
 * where the two do not fit (T31, 12 October 2026). "От лица:" is made the
 * same way: the same rule, and the list under its caption where the two do
 * not fit. Read off the stylesheet, which a document that lays nothing out
 * cannot show.
 */
describe("GameRoom lists of characters", () => {
  const rules = ruleMap(
    componentCss(join(dirname(fileURLToPath(import.meta.url)), "GameRoom.vue")),
  );

  it("stands the list of 'От лица' as wide as its longest name, not wider than the column", () => {
    expect(rules.get(".composer-as .composer-as-select")).toEqual({
      width: "auto",
      "max-width": "100%",
      "min-width": "0",
    });
    expect(rules.get(".composer-as .composer-as-select")).toEqual(
      rules.get(".pending-add .pending-select"),
    );
    // No limit of its own left on the list.
    expect(rules.get(".composer-as-select")).toBeUndefined();
  });

  it("puts the list under its caption where the two do not fit in a line", () => {
    expect(rules.get(".composer-as")).toMatchObject({
      display: "flex",
      "flex-wrap": "wrap",
    });
  });
});

/**
 * The form of a post as the old site lays it out: no frame and no plate of a
 * title over it, its captions in the old site's words and order, the dice
 * under the text with "Очистить" in their caption, the turn the master waits
 * for as the last field, and "Отправить".
 */
describe("GameRoom form of a post", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetRooms.mockResolvedValue({
      data: {
        resources: [{ ...room, settings: { diceEnabled: true } }],
        paging: null,
      },
      error: null,
    });
    mockGetPosts.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockGetCharacters.mockResolvedValue({
      data: { resources: [character], paging: null },
      error: null,
    });
    mockMarkRoomAsRead.mockResolvedValue({ data: null, error: null });
    mockGetFirstUnreadRoomPost.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "" },
    });
  });

  it("captions its fields in the old site's words and order, with no title over them", async () => {
    // The words of the stubbed button are its slot, drawn for this read.
    config.global.renderStubDefaultSlot = true;
    const wrapper = await mountRoom().finally(() => {
      config.global.renderStubDefaultSlot = false;
    });

    const captions = wrapper
      .get(".composer")
      .findAll(".composer-label, .dice-title, .attachments-title, .turns-title")
      // The space a template leaves at the start of the button is the start
      // of a line in its box, and a browser draws none there.
      .map((one) => one.text().replace(/\s+/g, " ").replace("( ", "("));
    expect(captions).toEqual([
      "Текст сообщения:",
      "Броски кубиков (Очистить):",
      "Комментарий:",
      "Вложение",
      "Я жду хода от:",
    ]);
    expect(wrapper.get(".composer-submit").text()).toBe("Отправить");
    expect(wrapper.text()).not.toContain("Новый пост");
  });

  // The old site's fields hold no hint: the caption over a field says it.
  it("puts no hint inside its fields", async () => {
    const wrapper = await mountRoom();

    expect(
      wrapper
        .findAllComponents({ name: "BBCodeEditor" })
        .map((editor) => editor.props("placeholder")),
    ).toEqual(["", ""]);
  });

  it("takes every roll back by Очистить", async () => {
    const wrapper = await mountRoom();
    const dice = wrapper.findComponent({ name: "DiceComposer" });
    await dice.vm.$emit("update:modelValue", [{ dice: 20, count: 1 }]);
    expect(dice.props("modelValue")).toHaveLength(1);

    await wrapper.get(".dice-clear").trigger("click");

    expect(dice.props("modelValue")).toEqual([]);
  });

  const rules = ruleMap(
    componentCss(join(dirname(fileURLToPath(import.meta.url)), "GameRoom.vue")),
  );

  it("stands on the page without a frame or a ground of its own", () => {
    expect(rules.get(".composer")).toEqual({ "margin-top": "32px" });
    expect(rules.get(".turns")).toEqual({ "margin-top": "16px" });
  });
});

/**
 * A room the reader may not open has no page of its own: the address opens
 * the site's "not found", at the address of the room, as any address that
 * leads nowhere does. Its posts are not asked for at all: the listing of the
 * game already said no.
 */
describe("GameRoom closed to the reader", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPosts.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockGetCharacters.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    mockMarkRoomAsRead.mockResolvedValue({ data: null, error: null });
    mockGetFirstUnreadRoomPost.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "" },
    });
  });

  const notFound = {
    name: "not-found",
    params: { pathMatch: ["game", "g-1", "rooms", "1"] },
    query: {},
    hash: "",
  };

  it("opens the site's not found for a closed room it may not open", async () => {
    mockGetRooms.mockResolvedValue({
      data: {
        resources: [{ ...room, canView: false, accesses: [], pendencies: [] }],
        paging: null,
      },
      error: null,
    });
    await mountRoom();

    expect(mockReplace).toHaveBeenCalledWith(notFound);
    expect(mockGetPosts).not.toHaveBeenCalled();
  });

  it("opens it for a room the list of the game does not have", async () => {
    mockGetRooms.mockResolvedValue({
      data: { resources: [{ ...room, roomNumber: 2 }], paging: null },
      error: null,
    });
    await mountRoom();

    expect(mockReplace).toHaveBeenCalledWith(notFound);
    expect(mockGetPosts).not.toHaveBeenCalled();
  });

  // A list that answered with nothing is an answer: a game with no rooms, or
  // one whose every room is closed and hidden from this reader.
  it("opens it when the list of the game answered with no room at all", async () => {
    mockGetRooms.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    const wrapper = await mountRoom();

    expect(mockReplace).toHaveBeenCalledWith(notFound);
    expect(mockGetPosts).not.toHaveBeenCalled();
    expect(wrapper.findComponent({ name: "ErrorState" }).exists()).toBe(false);
  });

  it("opens the room it may open", async () => {
    mockGetRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    await mountRoom();

    expect(mockReplace).not.toHaveBeenCalledWith(
      expect.objectContaining({ name: "not-found" }),
    );
    expect(mockGetPosts).toHaveBeenCalled();
  });

  it("does not send the reader away when the list itself failed", async () => {
    mockGetRooms.mockResolvedValue({ data: null, error: refusal(500) });
    await mountRoom();

    expect(mockReplace).not.toHaveBeenCalledWith(
      expect.objectContaining({ name: "not-found" }),
    );
  });
});
