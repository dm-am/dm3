/**
 * The board of the TopicsList specs, what the server answers for it, and the
 * helpers that read the listing. openBoard mounts it on the real router, store
 * and table, over the forum client of TopicsList.doubles.ts.
 */
import { vi } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { useBoardsStore } from "@/entities/forum";
import TopicsList from "./TopicsList.vue";
import { mockGetTopics } from "./TopicsList.doubles";

/** A status the response interceptor stays quiet about, so the page must speak. */
export const refusal = (status: number): GeneralError => ({
  type: "",
  title: "",
  status,
  traceId: "trace",
});

export const board = {
  id: "b-1",
  alias: "main",
  title: "Общий",
  moderators: [{ username: "moder" }],
  // What the server answers a member on an ordinary board.
  viewer: { canCreateTopic: true, canCreateTopicAfterSignIn: false },
};

export const topic = (id: string, title: string, isAttached: boolean) => ({
  id,
  topicNumber: isAttached ? 1 : 2,
  title,
  isAttached,
  isClosed: false,
  commentsCount: 0,
  unreadCommentsCount: 0,
  likesCount: 0,
  createdUtc: "2026-07-01T10:00:00Z",
  lastComment: null,
  author: { username: "author", role: "RegularUser" },
});

type Row = ReturnType<typeof topic>;

export const RULES = topic("t-rules", "Правила", true);
export const NEWS = topic("t-news", "Объявления", true);
export const PLAIN = topic("t-plain", "Болталка", false);

/** The topic as the server answers it once pinned, or once unpinned. */
export const pinned = (row: Row): Row => ({ ...row, isAttached: true });
export const unpinned = (row: Row): Row => ({ ...row, isAttached: false });

/** What the server answers for the board from now on: the pinned block and the rest of page 1. */
export function answerBoard(block: object[], rest: object[]) {
  mockGetTopics.mockImplementation(
    (_alias: string, query?: { isAttached?: boolean }) =>
      Promise.resolve(
        query?.isAttached
          ? { data: { resources: block, paging: null }, error: null }
          : {
              data: {
                resources: rest,
                paging: { current: 1, skip: 0, take: 20, total: 1, pages: 1 },
              },
              error: null,
            },
      ),
  );
}

export const messages = () => useToast().toasts.value.map((t) => t.message);

/** No plate left over from the test before, no call on the mocks, nobody signed in. */
export function freshPage() {
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((t) => dismiss(t.id));
  localStorage.clear();
  vi.clearAllMocks();
}

/** The session the auth store reads when the page mounts. */
export const signIn = (username: string, role = "RegularUser") =>
  localStorage.setItem("user", JSON.stringify({ username, role }));

/** The board as its moderator reads it: the actions and the order of the pinned topics are his. */
export function moderatedBoard() {
  freshPage();
  signIn("moder");
  answerBoard([RULES, NEWS], [PLAIN]);
}

/**
 * The listing of the board the store holds, at its address: the real router,
 * table, tooltips and links, in the document, so the caret can be read off it.
 */
export async function openBoard(address = "/forum/main", held: object = board) {
  // jsdom has no 2D canvas, which a tooltip measures its words with: it measures none.
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
  const pinia = createPinia();
  setActivePinia(pinia);
  // Before mount: the listing's immediate watcher fetches for the board the store holds.
  useBoardsStore().selectedBoard = held as never;
  const { wrapper } = await mountPage(TopicsList, address, {
    pinia,
    attachTo: document.body,
    routes: [
      ["forum", "/forum/:alias"],
      ["topic", "/forum/:alias/:num"],
      ["profile", "/users/:username"],
    ],
  });
  await flushPromises();
  return wrapper;
}

/** The rows of the listing, top to bottom. */
export const rows = (wrapper: VueWrapper) => wrapper.findAll("tr.table-row");

/** The row of a topic, by the title its link shows. */
export function rowOf(wrapper: VueWrapper, row: { title: string }) {
  const found = rows(wrapper).find(
    (one) => one.get(".col-title .topic-link").text() === row.title,
  );
  if (!found) throw new Error(`no row for ${row.title}`);
  return found;
}

/** The ids of the listing's rows, top to bottom, as their pins name them. */
export const rowIds = (wrapper: VueWrapper) =>
  rows(wrapper).map((row) =>
    row.get("[data-pin-toggle]").attributes("data-pin-toggle"),
  );

/** A primary-button pointer event; jsdom has no PointerEvent of its own. */
export function pointer(target: Element, type: string, clientY: number) {
  target.dispatchEvent(
    new MouseEvent(type, {
      bubbles: true,
      cancelable: true,
      clientY,
      button: 0,
    }),
  );
}

/** Lays the rows out 40px apart: a drag finds its place by the middles of the rows. */
export function layRows(wrapper: VueWrapper) {
  rows(wrapper).forEach((row, index) => {
    vi.spyOn(row.element, "getBoundingClientRect").mockReturnValue({
      top: index * 40,
      height: 40,
      bottom: index * 40 + 40,
      left: 0,
      right: 600,
      width: 600,
      x: 0,
      y: index * 40,
      toJSON: () => ({}),
    });
  });
}

/** The pin before the title of a topic, by the topic's id. */
export const pinOf = (wrapper: VueWrapper, id: string) =>
  wrapper.get(`[data-pin-toggle="${id}"]`);

/** A click as the browser sends it for a press of the mouse. */
export const click = (target: Element) =>
  target.dispatchEvent(
    new MouseEvent("click", { bubbles: true, cancelable: true, detail: 1 }),
  );

/** What the tooltip over an element says while the pointer rests on it. */
export async function tipOf(wrapper: { element: Element }): Promise<string> {
  const trigger = wrapper.element.closest(".tooltip-trigger");
  if (!trigger) throw new Error("no tooltip over the element");
  trigger.dispatchEvent(new MouseEvent("mouseenter"));
  const id = await vi.waitFor(() => {
    const shown = trigger.getAttribute("aria-describedby");
    if (!shown) throw new Error("the tooltip has not opened");
    return shown;
  });
  const text = document.getElementById(id)?.textContent?.trim() ?? "";
  trigger.dispatchEvent(new MouseEvent("mouseleave"));
  await vi.waitFor(() => {
    if (trigger.hasAttribute("aria-describedby"))
      throw new Error("the tooltip has not closed");
  });
  return text;
}

// The shallow listing below is TopicCreateForm.spec.ts's, under the router of
// TopicsList.doubles.ts, until that spec mounts the form on openBoard.

let mounted: VueWrapper | null = null;

/** Takes the listing of the last test down. */
export function unmountList() {
  mounted?.unmount();
  mounted = null;
}

/** `stubs` replaces the stubs below for one test, `held` the board the store holds. */
export async function mountList(
  stubs: Record<string, unknown> = {},
  held: object = board,
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  // Before mount: the listing's immediate watcher fetches for the board the store holds.
  useBoardsStore().selectedBoard = held as never;

  const wrapper = mount(TopicsList, {
    shallow: true,
    // In the document, so the caret can be read off it.
    attachTo: document.body,
    global: {
      plugins: [pinia],
      stubs: {
        RouterLink: true,
        Tooltip: {
          props: ["text"],
          template: `<div class="tip" :data-tip="text"><slot /></div>`,
        },
        DataTable: {
          props: ["data", "columns", "detailsFor"],
          template: `<table><tbody><template v-for="(row, index) in data" :key="row.id"><tr class="row" :data-id="row.id"><td v-for="column in columns" :key="column.key" :class="'col-' + column.key"><slot :name="'cell-' + column.key" :row="row" :index="index" /></td></tr><tr v-if="detailsFor !== null && detailsFor === row.id" class="details" :data-details-for="row.id"><td><slot name="details" :row="row" :index="index" /></td></tr></template></tbody></table>`,
        },
        // Drawn for real: the assertions read its words.
        ActionResult: false,
        Button: {
          props: ["loading", "disabled"],
          template: `<button :disabled="loading || disabled"><slot /></button>`,
        },
        // Rendered: the assertions are about the sentence it is handed.
        ErrorState: {
          props: ["message"],
          template: `<div class="error-strip">{{ message }}</div>`,
        },
        TopicTitleCell: false,
        TopicCreateForm: false,
        ...stubs,
      },
    },
  });
  mounted = wrapper;
  await flushPromises();
  return wrapper;
}

/** A reader signed in, with no plate left over and no call on the mocks. */
export function signedInReader() {
  freshPage();
  signIn("reader");
}
