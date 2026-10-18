/**
 * @vitest-environment jsdom
 */

/**
 * The chat room of a game gets no push of its own: a message written by anyone
 * else showed up only after a reload, or as a side effect of the reader sending
 * something himself, so two people in the same room were talking past each
 * other until one of them refreshed.
 *
 * These pin the live tail API_DESIGN.md ("Realtime") states for every live
 * list: read the newest page again, de-duplicate by id, and re-read the room
 * whole when the tail shares nothing with what is shown. They pin the moments
 * it must NOT run as well, which is the half that costs something when it is
 * missing: a tab that is off screen, a page that has left, a room the server
 * has refused, a read that is already in flight, and an answer that belongs to
 * a room the reader has since left.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { defineComponent, h, type Ref } from "vue";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/shared/stores";
import { LIVE_TAIL_POLL_INTERVAL_MS } from "@/shared/api/client";
import type { Message } from "@/shared/api/models/common";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import {
  provideZoneMarker,
  provideZoneSection,
} from "@/shared/lib/composables/useZoneSection";
import RefusalNote from "@/shared/ui/Layout/RefusalNote.vue";
import GameChatRoom from "./GameChatRoom.vue";

const {
  mockGetChatMessages,
  mockSendChatMessage,
  mockMarkChatRoomRead,
  mockGetRooms,
  mockReplace,
} = vi.hoisted(() => ({
  mockGetChatMessages: vi.fn(),
  mockSendChatMessage: vi.fn(),
  mockMarkChatRoomRead: vi.fn(),
  mockGetRooms: vi.fn(),
  mockReplace: vi.fn(),
}));

vi.mock("@/entities/game/api/gameApi", () => ({
  default: {
    getChatMessages: mockGetChatMessages,
    sendChatMessage: mockSendChatMessage,
    markChatRoomRead: mockMarkChatRoomRead,
    getRooms: mockGetRooms,
  },
}));

/**
 * The address the page stands at. Its params are reactive because one of the
 * tests below moves the reader to another room of the same game, which is a
 * change of the room number in the address and nothing else. The path, the
 * query and the hash are what the site's "not found" opens at.
 */
const { route } = vi.hoisted(() => ({
  route: {
    params: { id: "g-1", num: "1" } as Record<string, string>,
    get path() {
      return `/game/${this.params.id}/chat-rooms/${this.params.num}`;
    },
    query: {} as Record<string, string>,
    hash: "",
  },
}));

vi.mock("vue-router", async () => {
  const { reactive } = await import("vue");
  route.params = reactive(route.params);
  return {
    useRoute: () => route,
    useRouter: () => ({ push: vi.fn(), replace: mockReplace }),
    RouterLink: { template: "<a><slot /></a>" },
  };
});

/** The site's "not found", opened at the address of the room. */
const notFoundAt = (num: string) => ({
  name: "not-found",
  params: { pathMatch: ["game", "g-1", "chat-rooms", num] },
  query: {},
  hash: "",
});

/** Whether the page has sent the reader to "not found". */
const sentToNotFound = () =>
  mockReplace.mock.calls.some(([to]) => to?.name === "not-found");

const message = (id: string): Message =>
  ({
    id,
    createdUtc: "2026-07-01T10:00:00Z",
    modifiedUtc: null,
    author: { username: "player" },
    text: id,
    isRemoved: false,
    deletedBy: null,
    deletedUtc: null,
    likes: [],
    edits: [],
  }) as unknown as Message;

/** One answer of the messages endpoint: the page and its cursors. */
const page = (...messages: Message[]) => ({
  data: {
    resources: messages,
    paging: {
      nextCursor: null,
      prevCursor: "cursor",
      hasNext: false,
      hasPrev: false,
    },
  },
  error: null,
});

/**
 * A read whose answer the test hands over when it chooses, so the state of the
 * page can be changed while the request is still in flight.
 */
function heldRead() {
  let answer!: (value: ReturnType<typeof page>) => void;
  const read = new Promise<ReturnType<typeof page>>((resolve) => {
    answer = resolve;
  });
  return { read, answer };
}

/** The messages the page is showing, oldest first. */
const shownIds = (wrapper: VueWrapper) =>
  wrapper.findAll(".chat-message-row").map((row) => row.attributes("id"));

/** How many reads of the message endpoint have happened so far. */
const reads = () => mockGetChatMessages.mock.calls.length;

/**
 * The flag the browser raises on the document, without the event that follows
 * it. Hiding a tab and telling the page about it are two separate tasks, and
 * a timer that comes due in between runs with the flag already up.
 */
function raiseTheHiddenFlag(hidden: boolean) {
  Object.defineProperty(document, "hidden", {
    value: hidden,
    configurable: true,
  });
}

/** Puts the tab on screen or off it, the way the browser reports it. */
function setHidden(hidden: boolean) {
  raiseTheHiddenFlag(hidden);
  document.dispatchEvent(new Event("visibilitychange"));
}

/**
 * The geometry of the message window. jsdom lays nothing out and reports all
 * three numbers as zero, which reads as "at the bottom", so the numbers this
 * page measures are written by hand: a window 1000 tall showing 300 of it, of
 * which the tail is 700 and the top is 0.
 */
const WINDOW_HEIGHT = 1000;
const VIEWPORT_HEIGHT = 300;
const TAIL_SCROLL_TOP = WINDOW_HEIGHT - VIEWPORT_HEIGHT;

function layOutTheWindow(wrapper: VueWrapper, scrollTop: number) {
  const el = wrapper.find(".chat-messages").element;
  Object.defineProperty(el, "scrollHeight", {
    value: WINDOW_HEIGHT,
    configurable: true,
  });
  Object.defineProperty(el, "clientHeight", {
    value: VIEWPORT_HEIGHT,
    configurable: true,
  });
  Object.defineProperty(el, "scrollTop", {
    value: scrollTop,
    writable: true,
    configurable: true,
  });
}

/** Puts the reader well above the tail. */
function parkAboveTheTail(wrapper: VueWrapper) {
  layOutTheWindow(wrapper, 0);
}

/** Puts the reader at the tail, in a window that really can be scrolled. */
function parkAtTheTail(wrapper: VueWrapper) {
  layOutTheWindow(wrapper, TAIL_SCROLL_TOP);
}

/** The reader scrolling back down to the tail of the room. */
function returnToTheTail(wrapper: VueWrapper) {
  const el = wrapper.find(".chat-messages").element;
  el.scrollTop = TAIL_SCROLL_TOP;
}

/** Where the reader is standing in the message window. */
const scrollTopOf = (wrapper: VueWrapper) =>
  (wrapper.find(".chat-messages").element as HTMLElement).scrollTop;

let room: VueWrapper | null = null;

async function mountRoom({ signedIn = true, access = "Open" } = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  // The rooms list is the shell's, and the page only resolves its room out of
  // it: seeded here so the load under test is the message one. The second room
  // is what the reader moves to when the address changes.
  useGameDetailsStore().rooms = [
    { id: "r-1", roomNumber: 1, title: "Таверна", access },
    { id: "r-2", roomNumber: 2, title: "Погреб", access },
  ] as never;
  // The tail is a signed-in reader's unless a test says otherwise; the guest
  // has tests of his own.
  useAuthStore().user = signedIn ? ({ username: "player" } as never) : null;

  room = mount(GameChatRoom, {
    shallow: true,
    global: { plugins: [pinia], stubs: { RouterLink: true } },
  });
  await flushPromises();
  return room;
}

/** Lets one live-tail tick fire and its request settle. */
async function tick() {
  await vi.advanceTimersByTimeAsync(LIVE_TAIL_POLL_INTERVAL_MS);
  await flushPromises();
}

describe("the live tail of a game chat room", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    route.params.id = "g-1";
    route.params.num = "1";
    setHidden(false);
    mockGetChatMessages.mockResolvedValue(page());
    mockSendChatMessage.mockResolvedValue({ data: null, error: null });
    mockMarkChatRoomRead.mockResolvedValue({ data: null, error: null });
  });

  afterEach(() => {
    room?.unmount();
    room = null;
    vi.useRealTimers();
    setHidden(false);
  });

  it("reads the newest page on every tick and shows a message once", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();
    expect(mockGetChatMessages).toHaveBeenCalledTimes(1);

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    await tick();

    expect(mockGetChatMessages).toHaveBeenCalledTimes(2);
    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);

    // The same page again adds nothing: the tail is de-duplicated by id.
    await tick();
    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
  });

  it("shows the window it already has when the tail shares nothing with it", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();

    // More than a page went by unseen: appending would leave a hole between
    // m-1 and what arrived, so the window is replaced instead.
    mockGetChatMessages.mockResolvedValue(
      page(message("m-9"), message("m-10")),
    );
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-9", "msg-m-10"]);
    // The page that decided the window had drifted off IS the window to show,
    // so it is applied where it landed. Asking the room for it a second time
    // cost a request, raised `loading` on a feed that was perfectly readable,
    // and blinked it into skeletons.
    expect(mockGetChatMessages).toHaveBeenCalledTimes(2);
  });

  /**
   * The window under a reader who has scrolled up into the history IS the page
   * he is reading. Replacing it took that page away and left him at the end of
   * a conversation he had not read, with nothing said about either.
   */
  it("leaves the window in place under a reader who has scrolled up", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();
    parkAboveTheTail(wrapper);

    mockGetChatMessages.mockResolvedValue(
      page(message("m-9"), message("m-10")),
    );
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-1"]);
    expect(reads()).toBe(2);
  });

  it("replaces it on the tick that finds him back at the tail", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();
    parkAboveTheTail(wrapper);

    mockGetChatMessages.mockResolvedValue(
      page(message("m-9"), message("m-10")),
    );
    await tick();
    expect(shownIds(wrapper)).toEqual(["msg-m-1"]);

    returnToTheTail(wrapper);
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-9", "msg-m-10"]);
  });

  it("reads nothing while the tab is off screen", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    await mountRoom();
    const readsBefore = reads();

    setHidden(true);
    // Not "the tick declines to read": no tick is armed at all. The count is
    // asserted rather than inferred from the reads below, because the two
    // halves of this rule hide each other. A timer left running over a hidden
    // tab reads nothing anyway — the read is guarded too — so counting reads
    // says nothing about whether the timer was stopped, and a room that keeps
    // asking the browser for a tick every thirty seconds for as long as the
    // tab stays away is exactly what stopping it is for.
    expect(vi.getTimerCount()).toBe(0);
    await tick();
    await tick();

    expect(reads()).toBe(readsBefore);

    setHidden(false);
    await flushPromises();

    // And the return reads once, on its own, with no tick allowed to fire;
    // the timer comes back with it, and only one of it.
    expect(reads()).toBe(readsBefore + 1);
    expect(vi.getTimerCount()).toBe(1);
  });

  /**
   * The read is guarded as well as the timer, and this is the gap the guard is
   * for: the browser hides the tab and tells the page about it in two separate
   * tasks, so an interval that came due in between runs with the tab already
   * off screen. The event is deliberately not dispatched here — that is the
   * state of the page inside the gap.
   */
  it("reads nothing on a tick that fires after the tab is already hidden", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    await mountRoom();
    const readsBefore = reads();

    raiseTheHiddenFlag(true);
    await tick();
    await tick();

    expect(reads()).toBe(readsBefore);
  });

  it("catches up the moment the tab comes back", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();
    setHidden(true);

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    setHidden(false);
    await flushPromises();

    // No tick was allowed to fire: the return itself did the reading.
    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
  });

  it("keeps a single timer when the tab is reported visible twice", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    await mountRoom();

    // One return to the screen can arrive as more than one event. Each of them
    // reads at once, which is the point of the event; what must not survive it
    // is a second interval running beside the first, doubling every tick from
    // then on.
    setHidden(false);
    setHidden(false);
    await flushPromises();
    const readsBefore = reads();

    await tick();

    expect(reads()).toBe(readsBefore + 1);
  });

  it("reads once when the tab comes back while a tick is in flight", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();
    const readsBefore = reads();

    const held = heldRead();
    mockGetChatMessages.mockReturnValueOnce(held.read);
    await tick();
    expect(reads()).toBe(readsBefore + 1);

    // The reader steps away and comes straight back while the tick is still
    // waiting for its answer. Two reads of the same room at once are two
    // answers de-duplicated against the same window.
    setHidden(true);
    setHidden(false);
    await flushPromises();
    expect(reads()).toBe(readsBefore + 1);

    // And the one read still lands: the flag is released whatever happens.
    held.answer(page(message("m-1"), message("m-2")));
    await flushPromises();
    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
  });

  it("drops the tail of a room the reader has already left", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();

    const held = heldRead();
    mockGetChatMessages.mockReturnValueOnce(held.read);
    await tick();
    expect(mockGetChatMessages.mock.calls.at(-1)?.[0]).toBe("r-1");

    // The reader opens another room of the same game while that read is in
    // flight. The empty page below is room 2, which has nothing in it.
    route.params.num = "2";
    await flushPromises();
    expect(shownIds(wrapper)).toEqual([]);

    held.answer(page(message("m-1"), message("m-2")));
    await flushPromises();

    // Room 1's messages under room 2's title is what applying it would mean.
    expect(shownIds(wrapper)).toEqual([]);
  });

  it('opens "not found" when the tail comes back refused, and reads no more', async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();

    // Admission ended while the room was open: the room was closed, or the
    // reader's row was taken away. A room that is no longer within the
    // reader's reach answers 404 (RoomAvailable turns it away before any rule
    // is asked), not 403.
    mockGetChatMessages.mockResolvedValueOnce({
      data: null,
      error: { status: 404 },
    });
    await tick();

    // What the initial load does for a refusal: the room has no page for him,
    // and the messages he may no longer read are not left on screen.
    expect(mockReplace).toHaveBeenCalledWith(notFoundAt("1"));
    expect(wrapper.find(".chat-messages").exists()).toBe(false);

    // And nothing asks again: a tick that kept asking put the interceptor's
    // toast on screen once every interval, for as long as the tab stayed open.
    const readsBefore = reads();
    await tick();
    await tick();
    expect(reads()).toBe(readsBefore);
  });

  /**
   * The refusal belongs to the room it was given for, and nothing ever put it
   * back: the next room the reader opened made its request, got its messages,
   * and showed him the refusal of the room he had left. The retry button of the
   * refused room itself was in the same position.
   */
  it("opens the next room of the game after one the server refused", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();

    mockGetChatMessages.mockResolvedValueOnce({
      data: null,
      error: { status: 404 },
    });
    await tick();
    expect(mockReplace).toHaveBeenCalledTimes(1);

    mockGetChatMessages.mockResolvedValue(page(message("m-7")));
    route.params.num = "2";
    await flushPromises();

    expect(mockReplace).toHaveBeenCalledTimes(1);
    expect(shownIds(wrapper)).toEqual(["msg-m-7"]);
  });

  /**
   * A load started while another is in flight makes the earlier one's answer
   * stale. Applied late, the refusal of the room the reader has left shut the
   * room he moved on to, which he may read.
   */
  it("drops the answer of a load a newer one has replaced", async () => {
    const held = heldRead();
    mockGetChatMessages.mockReturnValueOnce(held.read);
    const wrapper = await mountRoom();
    expect(mockGetChatMessages.mock.calls.at(-1)?.[0]).toBe("r-1");

    mockGetChatMessages.mockResolvedValueOnce(page(message("m-7")));
    route.params.num = "2";
    await flushPromises();
    expect(mockGetChatMessages.mock.calls.at(-1)?.[0]).toBe("r-2");
    expect(shownIds(wrapper)).toEqual(["msg-m-7"]);

    held.answer({ data: null, error: { status: 404 } } as never);
    await flushPromises();

    expect(mockReplace).not.toHaveBeenCalled();
    expect(shownIds(wrapper)).toEqual(["msg-m-7"]);
  });

  /**
   * A closed room is read to nobody who is not signed in, so the only answer
   * a guest could get is a refusal, and the client takes a 401 for a session
   * that ran out: "Сессия истекла, войдите снова" stood over every guest who
   * opened a room, on the first read and again on the ticks after it.
   */
  it("reads nothing for a guest of a closed room, neither on the way in nor on a tick", async () => {
    await mountRoom({ signedIn: false, access: "Private" });
    await tick();
    setHidden(true);
    setHidden(false);
    await flushPromises();
    await tick();

    expect(reads()).toBe(0);
  });

  // An open room is read by everybody who sees the game, and kept up to date
  // for a guest as for anybody.
  it("reads an open room for a guest, on the way in and on every tick", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom({ signedIn: false });
    expect(reads()).toBe(1);

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    await tick();

    expect(reads()).toBe(2);
    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
    // Nothing is marked read for nobody.
    expect(mockMarkChatRoomRead).not.toHaveBeenCalled();
  });

  it("changes nothing when a tail read fails, and asks again on the next tick", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();

    mockGetChatMessages.mockResolvedValueOnce({
      data: null,
      error: { status: 500 },
    });
    await tick();

    // A server that is having a bad minute is not a refusal: the room stays on
    // screen and the reader is told nothing.
    expect(shownIds(wrapper)).toEqual(["msg-m-1"]);
    expect(wrapper.find("div.chat-empty-inline").exists()).toBe(false);

    // Nor is a failure that carries no status at all: a dropped connection, a
    // request that never reached a server. The two branches of the refusal test
    // read a field that is not there, and only a status that IS 401 or 403
    // shuts the room.
    mockGetChatMessages.mockResolvedValueOnce({
      data: null,
      error: { title: "Не удалось выполнить запрос" },
    });
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-1"]);
    expect(wrapper.find("div.chat-empty-inline").exists()).toBe(false);

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
  });

  it("marks the room read when new messages land in front of the reader", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom({ signedIn: true });
    parkAtTheTail(wrapper);
    const marksBefore = mockMarkChatRoomRead.mock.calls.length;

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    await tick();

    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
    expect(mockMarkChatRoomRead.mock.calls.length).toBe(marksBefore + 1);
    // A reader standing at the tail is following the room, and the room is
    // followed down to what arrived.
    expect(scrollTopOf(wrapper)).toBe(WINDOW_HEIGHT);
  });

  it("leaves the room unread for a reader who is away from the tail", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom({ signedIn: true });
    parkAboveTheTail(wrapper);
    const marksBefore = mockMarkChatRoomRead.mock.calls.length;

    mockGetChatMessages.mockResolvedValue(page(message("m-1"), message("m-2")));
    await tick();

    // The message is on the page but below the fold, and the reader was not
    // taken to it. Clearing his unread mark would take away the one sign that
    // anything arrived.
    expect(shownIds(wrapper)).toEqual(["msg-m-1", "msg-m-2"]);
    expect(mockMarkChatRoomRead.mock.calls.length).toBe(marksBefore);
    // And he is left standing where he was. The mark and the scroll are two
    // separate consequences of the same guard, and the scroll is the one the
    // reader feels: a line appended below the fold that yanks the page down
    // takes the paragraph he was reading off the screen mid-sentence.
    expect(scrollTopOf(wrapper)).toBe(0);
  });

  it("stops reading once the page is gone", async () => {
    mockGetChatMessages.mockResolvedValueOnce(page(message("m-1")));
    const wrapper = await mountRoom();
    const readsBefore = reads();

    wrapper.unmount();
    room = null;
    await tick();
    await tick();

    expect(reads()).toBe(readsBefore);

    // The listener goes with it: a tab returning to the screen after the reader
    // has left this page must not start the room reading again.
    setHidden(true);
    setHidden(false);
    await flushPromises();
    await tick();

    expect(reads()).toBe(readsBefore);
  });
});

/**
 * The game's blacklist closes the discussion and the application, not the
 * rooms: who writes in a chat room is the room's grant to say. The viewer the
 * list holds used to get a sentence about the list in place of the field.
 */
describe("the field of a game chat room", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    route.params.id = "g-1";
    route.params.num = "1";
    setHidden(false);
    mockGetChatMessages.mockResolvedValue(page(message("m-1")));
    mockMarkChatRoomRead.mockResolvedValue({ data: null, error: null });
  });

  afterEach(() => {
    room?.unmount();
    room = null;
  });

  async function mountAs(viewerBlacklisted: boolean) {
    const pinia = createPinia();
    setActivePinia(pinia);
    const store = useGameDetailsStore();
    // Let in to write: the room's grant is the whole of what this is about.
    store.rooms = [
      {
        id: "r-1",
        roomNumber: 1,
        title: "Таверна",
        access: "Open",
        accesses: [
          {
            id: "a-1",
            policy: "Full",
            character: null,
            user: { username: "player" },
          },
        ],
      },
    ] as never;
    store.game = { id: "g-guid", publicId: "g-1", viewerBlacklisted } as never;
    useAuthStore().user = { username: "player" } as never;

    room = mount(GameChatRoom, {
      global: {
        plugins: [pinia],
        stubs: {
          RouterLink: true,
          BBCodeEditor: true,
          ChatMessage: true,
          LoginPrompt: true,
        },
      },
    });
    await flushPromises();
    return room;
  }

  it("offers the field to the viewer on the game's list as to anybody", async () => {
    const wrapper = await mountAs(true);

    expect(wrapper.findComponent({ name: "BBCodeEditor" }).exists()).toBe(true);
    expect(wrapper.get(".chat-input-wrapper").text()).not.toContain(
      "черном списке",
    );
    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
  });
});

/**
 * Who gets the field. Openness is about reading (the owner, 12.10:
 * "Открытость - это про чтение. Писать могут только те, кого добавили."): an
 * open room is read by everybody who sees the game, and written in by the
 * master, the assistants and the people the room lets write - a reader row
 * with "Может писать", or the same on a character of the viewer's own that is
 * in play. The server asks exactly that before it takes a message
 * (RoomIntentionResolver.SendMessage), and a field it would refuse is not
 * offered: in its place stands the plaque of a refusal saying who writes
 * here, a plaque and not a grey line, so it never reads like an empty one.
 *
 * What each test catches:
 *  - the field offered to whoever may read: the viewer types a message and
 *    the server refuses it;
 *  - a grant read without its policy, or somebody else's grant read as the
 *    viewer's own, or a character that has left the game counted as in play;
 *  - the master and the assistants asked for a grant they do not need;
 *  - the guest of a closed room asked the server for a room it reads to
 *    nobody signed out, or given anything but the invitation to sign in and
 *    read; the guest of an open room kept from reading it, or offered a field
 *    or a way in that would let him write no more than he can now;
 *  - a room closed to the viewer shown as a page of the room (a failure, a
 *    plaque, the line of who writes) rather than the site's "not found",
 *    and that line drawn under a load that failed.
 */
describe("who gets the field of a game chat room", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    route.params.id = "g-1";
    route.params.num = "1";
    setHidden(false);
    mockGetChatMessages.mockResolvedValue(page(message("m-1")));
    mockMarkChatRoomRead.mockResolvedValue({ data: null, error: null });
  });

  afterEach(() => {
    room?.unmount();
    room = null;
  });

  // True of an open room and of a closed one alike: a reader added to a
  // closed room is in it and still may not write, so "those added to the
  // room" would name him among the writers.
  const WHO_WRITES =
    "Писать здесь могут мастер, ассистенты и те, кому мастер разрешил писать в этой комнате";

  const readerRow = (policy: string, username = "player") => ({
    id: `reader-${username}-${policy}`,
    policy,
    character: null,
    user: { username },
  });

  const characterRow = (
    policy: string,
    { author = "player", status = "Active" } = {},
  ) => ({
    id: `character-${author}-${policy}-${status}`,
    policy,
    character: {
      id: `c-${author}`,
      name: "Ян",
      status,
      author: { username: author },
    },
    user: { username: author },
  });

  async function mountAs({
    access = "Open",
    accesses = [] as unknown[],
    participation = [] as string[],
    signedIn = true,
    gameIn = true,
    canView = undefined as boolean | undefined,
  } = {}) {
    const pinia = createPinia();
    setActivePinia(pinia);
    const store = useGameDetailsStore();
    store.rooms = [
      { id: "r-1", roomNumber: 1, title: "Таверна", access, accesses, canView },
    ] as never;
    store.game = gameIn
      ? ({ id: "g-guid", publicId: "g-1", participation } as never)
      : null;
    useAuthStore().user = signedIn ? ({ username: "player" } as never) : null;

    room = mount(GameChatRoom, {
      global: {
        plugins: [pinia],
        stubs: {
          RouterLink: true,
          BBCodeEditor: true,
          ChatMessage: true,
        },
      },
    });
    await flushPromises();
    return room;
  }

  /**
   * The line of who writes, read only off a plaque (RefusalNote): set back in
   * grey, it is no line of who writes at all, and every case that expects one
   * goes red.
   */
  const whoWrites = (wrapper: VueWrapper) =>
    wrapper
      .findAllComponents(RefusalNote)
      .find((plaque) => plaque.classes().includes("chat-write-hint"));

  /** What stands under the messages. */
  const under = (wrapper: VueWrapper) => ({
    field: wrapper.findComponent({ name: "BBCodeEditor" }).exists(),
    send: wrapper.find(".chat-send-button").exists(),
    line: whoWrites(wrapper)?.text() ?? null,
    signIn: wrapper.find(".chat-input-wrapper .login-prompt").exists(),
  });

  const FIELD = { field: true, send: true, line: null, signIn: false };
  const LINE = { field: false, send: false, line: WHO_WRITES, signIn: false };
  const NOTHING = { field: false, send: false, line: null, signIn: false };

  it("gives a reader of an open room nobody let in the line, not the field", async () => {
    const wrapper = await mountAs();

    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
    expect(under(wrapper)).toEqual(LINE);
  });

  it("gives the field to a reader the room lets write", async () => {
    const wrapper = await mountAs({ accesses: [readerRow("Full")] });

    expect(under(wrapper)).toEqual(FIELD);
  });

  it("gives the line to a reader the room lets only read", async () => {
    const wrapper = await mountAs({
      access: "Private",
      accesses: [readerRow("ReadOnly")],
    });

    expect(under(wrapper)).toEqual(LINE);
  });

  it("gives the field to a player whose character in play the room lets write", async () => {
    const wrapper = await mountAs({ accesses: [characterRow("Full")] });

    expect(under(wrapper)).toEqual(FIELD);
  });

  it("does not count a character that has left the game", async () => {
    const wrapper = await mountAs({
      accesses: [characterRow("Full", { status: "Retired" })],
    });

    expect(under(wrapper)).toEqual(LINE);
  });

  it("does not count the grant of somebody else", async () => {
    const wrapper = await mountAs({
      accesses: [
        characterRow("Full", { author: "other" }),
        readerRow("Full", "other"),
      ],
    });

    expect(under(wrapper)).toEqual(LINE);
  });

  it("does not count a character the room lets only read", async () => {
    const wrapper = await mountAs({
      access: "Private",
      accesses: [characterRow("ReadOnly")],
    });

    expect(under(wrapper)).toEqual(LINE);
  });

  it("gives the field to the master and to an assistant without a grant", async () => {
    expect(
      under(await mountAs({ participation: ["Owner", "Authority"] })),
    ).toEqual(FIELD);
    room?.unmount();

    expect(under(await mountAs({ participation: ["Authority"] }))).toEqual(
      FIELD,
    );
  });

  // The role that takes the line away arrives with the game: drawn before it,
  // the line told a master under his own room that he may not write there,
  // and then gave way to his field.
  it("draws no line until the game, and the viewer's role in it, are in", async () => {
    const wrapper = await mountAs({ gameIn: false });

    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
    expect(under(wrapper)).toEqual(NOTHING);

    useGameDetailsStore().game = {
      id: "g-guid",
      publicId: "g-1",
      participation: ["Owner"],
    } as never;
    await flushPromises();

    expect(under(wrapper)).toEqual(FIELD);
  });

  // Nor while the messages load: the answer may yet be the refusal of a room
  // the viewer may not read at all, and the line would stand under it.
  it("draws no line while the messages are on their way", async () => {
    let answer: (value: unknown) => void = () => {};
    mockGetChatMessages.mockReturnValue(
      new Promise((resolve) => {
        answer = resolve;
      }),
    );
    const wrapper = await mountAs();

    expect(under(wrapper)).toEqual(NOTHING);

    answer(page(message("m-1")));
    await flushPromises();

    expect(under(wrapper)).toEqual(LINE);
  });

  /**
   * An open room is read by everybody who sees the game, a guest included,
   * and written in by the people the room names. Signing in lets nobody write
   * by itself, so under the messages the guest is told who writes, as a
   * signed-in reader is, and not invited in.
   */
  it("reads an open room to a guest and tells him who writes in it", async () => {
    const wrapper = await mountAs({ signedIn: false });

    expect(mockGetChatMessages).toHaveBeenCalledTimes(1);
    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
    expect(under(wrapper)).toEqual(LINE);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(sentToNotFound()).toBe(false);
  });

  /**
   * A closed room is read only by the people it was opened to, all of them
   * signed in. The list closes it to a guest (canView), and such a room has
   * no page: that case is the site's "not found" below. The list the guest
   * still holds may open it, though: the list of the viewer whose session has
   * just run out. That room is not asked for its messages either, and stands
   * as one plaque inviting him to sign in to read it. The line that stood
   * over it said the chat was for the members of the game, which is not the
   * rule.
   */
  it("invites the guest of a closed room his list opens to sign in to read, and asks the server nothing", async () => {
    const wrapper = await mountAs({ signedIn: false, access: "Private" });

    expect(mockGetChatMessages).not.toHaveBeenCalled();
    expect(wrapper.findAll(".login-prompt")).toHaveLength(1);
    expect(wrapper.get(".login-prompt").text()).toContain("читать чат игры");
    expect(wrapper.find(".chat-messages").exists()).toBe(false);
    expect(wrapper.find(".chat-input-wrapper").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("участникам игры");
    expect(sentToNotFound()).toBe(false);
  });

  /**
   * A room the server will not read to a guest (a 401) is one he is invited
   * to sign in to, not one said to be closed: what the answer says is that
   * the room is read to the signed-in.
   */
  it("invites the guest in when the server reads the room to the signed-in only", async () => {
    mockGetChatMessages.mockResolvedValue({
      data: null,
      error: { status: 401 },
    });
    const wrapper = await mountAs({ signedIn: false });

    expect(wrapper.findAll(".login-prompt")).toHaveLength(1);
    expect(wrapper.get(".login-prompt").text()).toContain("читать чат игры");
    expect(wrapper.find(".chat-messages").exists()).toBe(false);
    expect(sentToNotFound()).toBe(false);
  });

  // Signing in from that plaque does not reload the page, so the page itself
  // has to read the room for the viewer who has just arrived.
  it("reads the room the moment the guest signs in", async () => {
    // The server's own answer for a closed room: the messages for a viewer it
    // was opened to, a 401 for nobody.
    mockGetChatMessages.mockImplementation(async () =>
      useAuthStore().user
        ? page(message("m-1"))
        : { data: null, error: { status: 401 } },
    );
    const wrapper = await mountAs({ signedIn: false, access: "Private" });

    useAuthStore().user = { username: "player" } as never;
    await flushPromises();

    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
  });

  /**
   * A session that ran out answers 401, and the client's interceptor says so
   * and takes the viewer off the store. The room is then read as a guest's:
   * a closed one is the invitation to sign in, asked for nothing more, and an
   * open one is read again and stays on screen. The answer to the read the
   * session died under arrives after the guest's read has started, and must
   * not put its refusal over the open room.
   */
  it("turns a closed room into the guest's invitation when the session runs out", async () => {
    mockGetChatMessages.mockImplementation(async () => {
      useAuthStore().user = null;
      return { data: null, error: { status: 401 } };
    });
    const wrapper = await mountAs({ access: "Private" });

    expect(mockGetChatMessages).toHaveBeenCalledTimes(1);
    expect(wrapper.findAll(".login-prompt")).toHaveLength(1);
    expect(wrapper.get(".login-prompt").text()).toContain("читать чат игры");
    expect(wrapper.find(".chat-messages").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("участникам игры");
    expect(sentToNotFound()).toBe(false);
  });

  it("keeps an open room on screen for the guest when the session runs out", async () => {
    mockGetChatMessages.mockImplementation(async () => {
      if (!useAuthStore().user) return page(message("m-1"));
      useAuthStore().user = null;
      return { data: null, error: { status: 401 } };
    });
    const wrapper = await mountAs();

    expect(mockGetChatMessages).toHaveBeenCalledTimes(2);
    expect(wrapper.findAll(".chat-message-row")).toHaveLength(1);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(sentToNotFound()).toBe(false);
    expect(under(wrapper)).toEqual(LINE);
  });

  /**
   * A closed room at its address, for a viewer it is closed to, where the
   * list did not say so: closed while he read it, or his access taken away.
   * The server answers its messages 404 since the room is not within the
   * viewer's reach (RoomAvailable), and the page said "Не удалось загрузить
   * сообщения" with "Повторить" under it, blaming the site for its own rule
   * and asking for a pointless retry. Such a room has no page, as a room of
   * posts has none: the address opens the site's "not found", with nothing
   * of the room left on screen meanwhile.
   */
  it.each([404, 403])(
    'opens "not found" at the address of the room when its messages answer %s',
    async (status) => {
      mockGetChatMessages.mockResolvedValue({ data: null, error: { status } });
      const wrapper = await mountAs({ access: "Private" });

      expect(mockReplace).toHaveBeenCalledWith(notFoundAt("1"));
      expect(wrapper.findAllComponents(RefusalNote)).toHaveLength(0);
      expect(wrapper.find(".chat-messages").exists()).toBe(false);
      expect(wrapper.find(".chat-input-wrapper").exists()).toBe(false);
      expect(wrapper.find(".login-prompt").exists()).toBe(false);
      expect(wrapper.text()).not.toContain("закрыта");
      expect(wrapper.text()).not.toContain("Не удалось загрузить");
      expect(wrapper.text()).not.toContain("Повторить");
    },
  );

  /**
   * A closed room the list closes to the viewer (canView false), a guest or
   * signed in, and a number the list does not hold: a room the viewer may not
   * open has no page, so the address opens the site's "not found", as an
   * address of a room of posts does (GameRoom.showNotFound), at the same
   * address with its query and hash. The server is not asked for messages it
   * would refuse.
   */
  it.each([
    ["a signed-in viewer", true],
    ["a guest", false],
  ] as const)(
    'opens "not found" for %s of a room the list closes to him',
    async (_who, signedIn) => {
      const wrapper = await mountAs({
        access: "Private",
        canView: false,
        signedIn,
      });

      expect(mockReplace).toHaveBeenCalledWith(notFoundAt("1"));
      expect(mockGetChatMessages).not.toHaveBeenCalled();
      expect(wrapper.find(".login-prompt").exists()).toBe(false);
      expect(wrapper.findAllComponents(RefusalNote)).toHaveLength(0);
      expect(wrapper.find(".chat-messages").exists()).toBe(false);
      expect(wrapper.text()).not.toContain("закрыта");
    },
  );

  it('opens "not found" for a number the list does not hold, with the query and the hash', async () => {
    route.params.num = "7";
    route.query = { scrollTo: "m-1" };
    route.hash = "#msg-m-1";
    try {
      const wrapper = await mountAs();

      expect(mockReplace).toHaveBeenCalledWith({
        ...notFoundAt("7"),
        query: { scrollTo: "m-1" },
        hash: "#msg-m-1",
      });
      expect(mockGetChatMessages).not.toHaveBeenCalled();
      expect(wrapper.text()).not.toContain("не найдена");
      expect(wrapper.find(".chat-messages").exists()).toBe(false);
    } finally {
      route.query = {};
      route.hash = "";
    }
  });

  // Until the game is in, the list may be another game's: nothing is
  // decided, and the frame waits in its skeleton.
  it("decides nothing about a room it cannot find until the game is in", async () => {
    route.params.num = "7";
    const wrapper = await mountAs({ gameIn: false });

    expect(sentToNotFound()).toBe(false);
    expect(
      wrapper.findComponent({ name: "ChatMessageSkeleton" }).exists(),
    ).toBe(true);

    useGameDetailsStore().game = { id: "g-guid", publicId: "g-1" } as never;
    await flushPromises();

    expect(mockReplace).toHaveBeenCalledWith(notFoundAt("7"));
  });

  // On the way to another game the store still holds the game before and its
  // list for a moment: a room that list closes, or does not hold, says
  // nothing of the room at this address.
  it.each([
    ["closes", "1", false],
    ["does not hold", "7", undefined],
  ] as const)(
    "decides nothing about the room off a list of another game that %s it",
    async (_what, num, canView) => {
      route.params.num = num;
      await mountAs({ gameIn: false, canView });

      useGameDetailsStore().game = { id: "g2-guid", publicId: "g-2" } as never;
      await flushPromises();

      expect(sentToNotFound()).toBe(false);

      useGameDetailsStore().game = { id: "g-guid", publicId: "g-1" } as never;
      await flushPromises();

      expect(mockReplace).toHaveBeenCalledWith(notFoundAt(num));
    },
  );

  /**
   * The frame waits in the skeleton of what is coming: rows of a chat in the
   * full look (avatar 72, the name and the time, the lines), as the site's
   * chat waits, and the lines of the compact look in the compact one. The
   * card of a full comment, with its column of 140 and square of 128, is no
   * row of 88: the feed would jump the moment the rows came.
   */
  it.each([
    ["full", "ChatMessageSkeleton", "CommentSkeleton"],
    ["compact", "CommentSkeleton", "ChatMessageSkeleton"],
  ] as const)(
    "waits for the messages of the %s look in the skeleton of its rows",
    async (layout, shown, notShown) => {
      localStorage.setItem("dm_message_layout", layout);
      try {
        mockGetChatMessages.mockReturnValue(new Promise(() => {}));
        const wrapper = await mountAs();

        expect(wrapper.findComponent({ name: shown }).exists()).toBe(true);
        expect(wrapper.findComponent({ name: notShown }).exists()).toBe(false);
      } finally {
        localStorage.removeItem("dm_message_layout");
      }
    },
  );

  it("sets the rows of the skeleton where the rows of the feed stand", () => {
    const rules = styleRules(
      componentCss(
        resolve(dirname(fileURLToPath(import.meta.url)), "GameChatRoom.vue"),
      ),
    );
    const padding = (selector: string) =>
      rules.find((rule) => rule.selector === selector)?.declarations.padding;

    expect(padding(".chat-skeleton :deep(.skeleton-message)")).toBe(
      padding(".chat-message-row"),
    );
  });

  // A list that failed to load says nothing of the room: the reader is not
  // sent away, and the failure stands where the messages would, with the
  // retry that asks for the list again.
  it("does not send the reader away when the list itself failed", async () => {
    mockGetRooms.mockResolvedValue({ data: null, error: { status: 500 } });
    const pinia = createPinia();
    setActivePinia(pinia);
    useGameDetailsStore().game = { id: "g-guid", publicId: "g-1" } as never;
    useAuthStore().user = { username: "player" } as never;
    room = mount(GameChatRoom, {
      global: {
        plugins: [pinia],
        stubs: { RouterLink: true, BBCodeEditor: true, ChatMessage: true },
      },
    });
    await flushPromises();

    expect(sentToNotFound()).toBe(false);
    expect(room.get(".chat-error").text()).toContain(
      "Не удалось загрузить комнаты",
    );
    expect(mockGetChatMessages).not.toHaveBeenCalled();

    mockGetRooms.mockResolvedValue({
      data: {
        resources: [
          { id: "r-1", roomNumber: 1, title: "Таверна", access: "Open" },
        ],
        paging: null,
      },
      error: null,
    });
    await room.get(".chat-retry").trigger("click");
    await flushPromises();

    expect(sentToNotFound()).toBe(false);
    expect(room.findAll(".chat-message-row")).toHaveLength(1);
  });

  /**
   * The room holds the height of a feed (200) only where it draws one. A
   * closed room's guest and a viewer the room is closed to get one plaque,
   * and the height stood under it as an empty gap down to the next block.
   * The plaque and its own spacing are not the room's to change
   * (emptyStateAlignment.spec.ts).
   */
  it.each([
    [
      "a guest of a closed room",
      { signedIn: false, access: "Private" },
      null,
      false,
    ],
    ["a viewer the server turns away", { access: "Private" }, 404, false],
    ["a guest of an open room", { signedIn: false }, null, true],
    ["a reader of the room", {}, null, true],
  ] as const)(
    "keeps the height of a feed for %s only where there is one",
    async (_who, as, refusal, feed) => {
      if (refusal)
        mockGetChatMessages.mockResolvedValue({
          data: null,
          error: { status: refusal },
        });
      const wrapper = await mountAs(as);

      expect(wrapper.find(".chat-messages").exists()).toBe(feed);
      expect(wrapper.get(".chat-room").classes().includes("with-feed")).toBe(
        feed,
      );
    },
  );

  it("draws that height on a room with a feed, and on no other", () => {
    const rules = styleRules(
      componentCss(
        resolve(dirname(fileURLToPath(import.meta.url)), "GameChatRoom.vue"),
      ),
    );
    const on = (selector: string) =>
      rules
        .filter((rule) => rule.selector === selector)
        .map((rule) => rule.declarations["min-height"])
        .filter(Boolean);

    expect(on(".chat-room")).toEqual([]);
    expect(on(".chat-room.with-feed")).toEqual(["200px"]);
  });

  // A failed load is not a closed room: the error and its retry stay, and the
  // line of who writes does not stand under them.
  it("draws no line of who writes under a load that failed", async () => {
    mockGetChatMessages.mockResolvedValue({
      data: null,
      error: { status: 500 },
    });
    const wrapper = await mountAs();

    expect(wrapper.find(".chat-error").text()).toContain(
      "Не удалось загрузить сообщения",
    );
    expect(under(wrapper)).toEqual(NOTHING);
  });
});

/**
 * The name of a chat room stands once, in the heading of the zone ("ИГРА |
 * КОМНАТА"), as for a room of posts, with the archive marker after it. The
 * page drew it a second time under that heading, as "Чат: {name}": the name
 * twice, and a word for the kind of room nobody needs to read.
 */
describe("the heading of a game chat room", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    route.params.id = "g-1";
    route.params.num = "1";
    setHidden(false);
    mockGetChatMessages.mockResolvedValue(page(message("m-1")));
    mockMarkChatRoomRead.mockResolvedValue({ data: null, error: null });
  });

  afterEach(() => {
    room?.unmount();
    room = null;
  });

  it("names the room once, in the zone's heading, with its archive marker", async () => {
    const pinia = createPinia();
    setActivePinia(pinia);
    useGameDetailsStore().rooms = [
      { id: "r-1", roomNumber: 1, title: "Таверна", isArchived: true },
    ] as never;
    useAuthStore().user = { username: "player" } as never;

    let section!: Ref<string | undefined>;
    let marker!: Ref<string | undefined>;
    const Shell = defineComponent({
      setup() {
        section = provideZoneSection();
        marker = provideZoneMarker();
        return () => h(GameChatRoom);
      },
    });
    room = mount(Shell, {
      global: {
        plugins: [pinia],
        stubs: { RouterLink: true, BBCodeEditor: true, ChatMessage: true },
      },
    });
    await flushPromises();

    expect(section.value).toBe("Таверна");
    expect(marker.value).toBe("архив");
    expect(room.text()).not.toContain("Таверна");
    expect(room.text()).not.toContain("Чат");
  });
});

/**
 * The older messages of a room, read a page at a time as the reader scrolls up
 * to the top of the window: a sentinel above the oldest message, watched by an
 * IntersectionObserver, asks for the page before the oldest one, and the
 * reader stays on the message he was reading while the page lands above it.
 * jsdom has no observer, so the one the page builds is a stand-in the test
 * fires by hand.
 */
describe("the older messages of a game chat room", () => {
  const observers: {
    fire: (entries: { isIntersecting: boolean }[]) => void;
    watched: Element | null;
    live: boolean;
  }[] = [];

  /** The sentinel coming into view of the observer that watches it now. */
  const sentinelInView = () => {
    const current = observers.filter((o) => o.live && o.watched).at(-1);
    current?.fire([{ isIntersecting: true }]);
  };

  /** The reads that asked for a page before a cursor, oldest first. */
  const olderReads = () =>
    mockGetChatMessages.mock.calls.filter(([, cursor]) => cursor !== undefined);

  const withCursor = (hasPrev: boolean, ...messages: Message[]) => {
    const answer = page(...messages);
    answer.data.paging.hasPrev = hasPrev;
    answer.data.paging.prevCursor = hasPrev ? `before-${messages[0].id}` : "";
    return answer;
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    route.params.id = "g-1";
    route.params.num = "1";
    setHidden(false);
    observers.length = 0;
    vi.stubGlobal(
      "IntersectionObserver",
      class {
        record: (typeof observers)[number];
        constructor(
          callback: (entries: { isIntersecting: boolean }[]) => void,
        ) {
          this.record = { fire: callback, watched: null, live: true };
          observers.push(this.record);
        }
        observe(element: Element) {
          this.record.watched = element;
        }
        disconnect() {
          this.record.live = false;
        }
      },
    );
    mockMarkChatRoomRead.mockResolvedValue({ data: null, error: null });
    mockGetChatMessages.mockResolvedValue(
      withCursor(true, message("m-5"), message("m-6")),
    );
  });

  afterEach(() => {
    room?.unmount();
    room = null;
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it("reads the page before the oldest message when the top comes into view", async () => {
    const wrapper = await mountRoom();
    await flushPromises();
    mockGetChatMessages.mockResolvedValueOnce(
      withCursor(true, message("m-3"), message("m-4")),
    );

    sentinelInView();
    await flushPromises();

    expect(olderReads()).toEqual([["r-1", "before-m-5", 50]]);
    expect(shownIds(wrapper)).toEqual([
      "msg-m-3",
      "msg-m-4",
      "msg-m-5",
      "msg-m-6",
    ]);
  });

  it("keeps the reader on the message he was reading as the page lands above it", async () => {
    const wrapper = await mountRoom();
    await flushPromises();
    const el = wrapper.find(".chat-messages").element;
    let height = 1000;
    Object.defineProperty(el, "scrollHeight", {
      get: () => height,
      configurable: true,
    });
    el.scrollTop = 0;
    const { read, answer } = heldRead();
    mockGetChatMessages.mockReturnValueOnce(read);

    sentinelInView();
    height = 1600;
    answer(withCursor(true, message("m-3"), message("m-4")));
    await flushPromises();

    expect(el.scrollTop).toBe(600);
  });

  it("asks once while a page is on its way, and again only on the next sighting", async () => {
    await mountRoom();
    await flushPromises();
    const { read, answer } = heldRead();
    mockGetChatMessages.mockReturnValueOnce(read);

    sentinelInView();
    sentinelInView();
    expect(olderReads()).toHaveLength(1);

    answer(withCursor(true, message("m-3"), message("m-4")));
    await flushPromises();
    expect(olderReads()).toHaveLength(1);

    mockGetChatMessages.mockResolvedValueOnce(
      withCursor(false, message("m-1"), message("m-2")),
    );
    sentinelInView();
    await flushPromises();
    expect(olderReads().map(([, cursor]) => cursor)).toEqual([
      "before-m-5",
      "before-m-3",
    ]);
  });

  it("stops at the start of the room: no sentinel, no further read", async () => {
    const wrapper = await mountRoom();
    await flushPromises();
    mockGetChatMessages.mockResolvedValueOnce(
      withCursor(false, message("m-1"), message("m-2")),
    );

    sentinelInView();
    await flushPromises();

    expect(wrapper.find(".top-sentinel").exists()).toBe(false);
    sentinelInView();
    await flushPromises();
    expect(olderReads()).toHaveLength(1);
  });

  it("takes an empty page for the start of the room", async () => {
    const wrapper = await mountRoom();
    await flushPromises();
    mockGetChatMessages.mockResolvedValueOnce(withCursor(false));

    sentinelInView();
    await flushPromises();

    expect(wrapper.find(".top-sentinel").exists()).toBe(false);
    expect(shownIds(wrapper)).toEqual(["msg-m-5", "msg-m-6"]);
  });

  it("keeps the window and the sentinel when the read fails", async () => {
    const wrapper = await mountRoom();
    await flushPromises();
    mockGetChatMessages.mockResolvedValueOnce({
      data: null,
      error: { status: 500, title: "" },
    } as never);

    sentinelInView();
    await flushPromises();

    expect(shownIds(wrapper)).toEqual(["msg-m-5", "msg-m-6"]);
    expect(wrapper.find(".top-sentinel").exists()).toBe(true);
  });
});
