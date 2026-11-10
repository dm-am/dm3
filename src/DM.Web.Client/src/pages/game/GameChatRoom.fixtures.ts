// The data and the mount of the specs of a chat room; the specs unmount it.
import { vi } from "vitest";
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, type Pinia } from "pinia";
import type { Component } from "vue";
import type { Router } from "vue-router";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { LIVE_TAIL_POLL_INTERVAL_MS } from "@/shared/lib/utils/liveTail";
import type { Message } from "@/shared/api/models/common";
import { mountPage } from "@/shared/lib/testing/mountPage";
import RefusalNote from "@/shared/ui/Layout/RefusalNote.vue";
import GameChatRoom from "./GameChatRoom.vue";
import {
  mockGetChatMessages,
  mockMarkChatRoomRead,
} from "./GameChatRoom.doubles";

/** The address of a room of game g-1, with what follows its number. */
export const roomAt = (num: string, rest = "") =>
  `/game/g-1/chat-rooms/${num}${rest}`;

const routes: [string, string][] = [
  ["game", "/game/:id"],
  ["game-chat-room", "/game/:id/chat-rooms/:num"],
  ["not-found", "/:pathMatch(.*)*"],
];

let shown: VueWrapper | null = null;
let router: Router | null = null;
let notFoundAsks: { name: unknown; fullPath: string }[] = [];

/** Mounts the room at its address and keeps it until unmountRoom. */
export async function mountRoom(
  unit: Component,
  {
    pinia,
    stubs,
    at = roomAt("1"),
  }: { pinia: Pinia; stubs: Record<string, boolean>; at?: string },
) {
  notFoundAsks = [];
  const mounted = await mountPage(unit, at, {
    routes,
    pinia,
    stubs,
    // Held: in the app "not found" replaces the room, here the room stays read.
    beforeMount: (r) =>
      r.beforeEach((to) => {
        if (to.name !== "not-found") return;
        notFoundAsks.push({ name: to.name, fullPath: to.fullPath });
        return false;
      }),
  });
  shown = mounted.wrapper;
  router = mounted.router;
  await flushPromises();
  return mounted.wrapper;
}

export function unmountRoom() {
  shown?.unmount();
  shown = null;
  router = null;
}

/** The reader moves to another room of the game. */
export const moveTo = (num: string) => router!.push(roomAt(num));

/** Where the reader stands: the name of the route and the whole address. */
export const landing = () => ({
  name: router!.currentRoute.value.name,
  fullPath: router!.currentRoute.value.fullPath,
});

/** "Not found" drawn at the address of the room, query and hash kept. */
export const notFoundAt = (num: string, rest = "") => ({
  name: "not-found",
  fullPath: roomAt(num, rest),
});

/** Where the page last sent the reader to "not found", resolved by the router. */
export const notFoundAsked = () => notFoundAsks.at(-1) ?? null;

/** How many times the page has sent the reader to "not found". */
export const timesSentToNotFound = () => notFoundAsks.length;

/** Whether the page has sent the reader to "not found". */
export const sentToNotFound = () => notFoundAsks.length > 0;

export const message = (id: string): Message =>
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

export const page = (...messages: Message[]) => ({
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

/** A read the test answers when it chooses, to change the page meanwhile. */
export function heldRead() {
  let answer!: (value: ReturnType<typeof page>) => void;
  const read = new Promise<ReturnType<typeof page>>((resolve) => {
    answer = resolve;
  });
  return { read, answer };
}

/** The messages the page is showing, oldest first. */
export const shownIds = (wrapper: VueWrapper) =>
  wrapper.findAll(".chat-message-row").map((row) => row.attributes("id"));

export const reads = () => mockGetChatMessages.mock.calls.length;

// Without the event: a timer may come due between the flag and the event.
export function raiseTheHiddenFlag(hidden: boolean) {
  Object.defineProperty(document, "hidden", {
    value: hidden,
    configurable: true,
  });
}

export function setHidden(hidden: boolean) {
  raiseTheHiddenFlag(hidden);
  document.dispatchEvent(new Event("visibilitychange"));
}

/** Room 1 of game g-1, the tab on screen, one message in the room. */
export function standInRoomOne() {
  vi.clearAllMocks();
  setHidden(false);
  mockGetChatMessages.mockResolvedValue(page(message("m-1")));
  mockMarkChatRoomRead.mockResolvedValue({ data: null, error: null });
}

// jsdom reports every measure as zero, which reads as "at the bottom".
export const WINDOW_HEIGHT = 1000;
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
export function parkAboveTheTail(wrapper: VueWrapper) {
  layOutTheWindow(wrapper, 0);
}

export function parkAtTheTail(wrapper: VueWrapper) {
  layOutTheWindow(wrapper, TAIL_SCROLL_TOP);
}

export function returnToTheTail(wrapper: VueWrapper) {
  const el = wrapper.find(".chat-messages").element;
  el.scrollTop = TAIL_SCROLL_TOP;
}

export const scrollTopOf = (wrapper: VueWrapper) =>
  (wrapper.find(".chat-messages").element as HTMLElement).scrollTop;

// Every child of the room, as a shallow mount would stub them.
const CHILDREN = {
  ChatMessage: true,
  LoginPrompt: true,
  BBCodeEditor: true,
  DashSeparator: true,
  ErrorState: true,
  RefusalNote: true,
  BackLink: true,
  ChatMessageSkeleton: true,
  CommentSkeleton: true,
  Button: true,
  SecondaryText: true,
};

/** The second room is the one the address moves to. */
export async function openRoom({ signedIn = true, access = "Open" } = {}) {
  const pinia = createPinia();
  useGameDetailsStore(pinia).rooms = [
    { id: "r-1", roomNumber: 1, title: "Таверна", access },
    { id: "r-2", roomNumber: 2, title: "Погреб", access },
  ] as never;
  useAuthStore(pinia).user = signedIn
    ? ({ username: "player" } as never)
    : null;

  return mountRoom(GameChatRoom, { pinia, stubs: CHILDREN });
}

/** The children a room's frame does not need drawn. */
export const FRAME_STUBS = { BBCodeEditor: true, ChatMessage: true };

export async function tick() {
  await vi.advanceTimersByTimeAsync(LIVE_TAIL_POLL_INTERVAL_MS);
  await flushPromises();
}

const WHO_WRITES =
  "Писать здесь могут мастер, ассистенты и те, кому мастер разрешил писать в этой комнате";

export const readerRow = (policy: string, username = "player") => ({
  id: `reader-${username}-${policy}`,
  policy,
  character: null,
  user: { username },
});

export const characterRow = (
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

export async function openRoomAs({
  access = "Open",
  accesses = [] as unknown[],
  participation = [] as string[],
  signedIn = true,
  gameIn = true,
  canView = undefined as boolean | undefined,
  blacklisted = false,
  at = roomAt("1"),
} = {}) {
  const pinia = createPinia();
  const store = useGameDetailsStore(pinia);
  store.rooms = [
    { id: "r-1", roomNumber: 1, title: "Таверна", access, accesses, canView },
  ] as never;
  store.game = gameIn
    ? ({
        id: "g-guid",
        publicId: "g-1",
        participation,
        viewerBlacklisted: blacklisted,
      } as never)
    : null;
  useAuthStore(pinia).user = signedIn
    ? ({ username: "player" } as never)
    : null;

  return mountRoom(GameChatRoom, { pinia, stubs: FRAME_STUBS, at });
}

// Off a plaque only: a grey line is no line of who writes.
const whoWrites = (wrapper: VueWrapper) =>
  wrapper
    .findAllComponents(RefusalNote)
    .find((plaque) => plaque.classes().includes("chat-write-hint"));

export const under = (wrapper: VueWrapper) => ({
  field: wrapper.findComponent({ name: "BBCodeEditor" }).exists(),
  send: wrapper.find(".chat-send-button").exists(),
  line: whoWrites(wrapper)?.text() ?? null,
  signIn: wrapper.find(".chat-input-wrapper .login-prompt").exists(),
});

export const FIELD = { field: true, send: true, line: null, signIn: false };
export const LINE = {
  field: false,
  send: false,
  line: WHO_WRITES,
  signIn: false,
};
export const NOTHING = { field: false, send: false, line: null, signIn: false };
