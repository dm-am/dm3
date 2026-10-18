/**
 * @vitest-environment jsdom
 */

/**
 * The moderator actions of a board listing.
 *
 * Pinning used to end in nothing, and it reports the refusal now, under the
 * row. It was a grey button in a column of its own past the dates, behind the
 * sideways scroll of the table and nowhere in sight on a board with nothing
 * pinned; the pin before every title is the toggle now, as the heart of a like
 * is, pale where the topic is not pinned. The order of the pinned topics was a
 * window, then a mode of the listing with its own buttons; now the pin of a
 * pinned topic is also what moves it: dragged within the block, or with the
 * arrow keys. The order is sent the moment the topic is let go, one save at a
 * time, and a refusal puts the block back and is said on a plate, since the
 * action has no button of its own to say it beside. A drag ends in a click on
 * the pin it started on, and that click is not a press.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type { GeneralError } from "@/shared/api/models/common";
import { useToast } from "@/shared/lib/composables/useToast";
import { useBoardsStore } from "@/entities/forum";
import { useAuthStore } from "@/shared/stores";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import TopicsList from "./TopicsList.vue";

const {
  mockGetTopics,
  mockUpdateTopic,
  mockReorderPinnedTopics,
  mockCreateTopic,
  mockGetBoard,
  routeQuery,
} = vi.hoisted(() => ({
  mockGetTopics: vi.fn(),
  mockUpdateTopic: vi.fn(),
  mockReorderPinnedTopics: vi.fn(),
  mockCreateTopic: vi.fn(),
  mockGetBoard: vi.fn(),
  /** The query of the address; empty but where a test sorts the listing. */
  routeQuery: {} as Record<string, string>,
}));

vi.mock("@/entities/forum/api/forumApi", () => ({
  default: {
    getTopics: mockGetTopics,
    updateTopic: mockUpdateTopic,
    reorderPinnedTopics: mockReorderPinnedTopics,
    createTopic: mockCreateTopic,
    getBoard: mockGetBoard,
    getBoards: vi.fn(),
    getNews: vi.fn(),
  },
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { alias: "main" }, query: routeQuery }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  RouterLink: { template: "<a><slot /></a>" },
}));

/** A status the response interceptor stays quiet about, so the page must speak. */
const refusal = (status: number): GeneralError => ({
  type: "",
  title: "",
  status,
  traceId: "trace",
});

const board = {
  id: "b-1",
  alias: "main",
  title: "Общий",
  moderators: [{ username: "moder" }],
  // What the server answers for a member on an ordinary board.
  viewer: { canCreateTopic: true, canCreateTopicAfterSignIn: false },
};

const topic = (id: string, title: string, isAttached: boolean) => ({
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

const RULES = topic("t-rules", "Правила", true);
const NEWS = topic("t-news", "Объявления", true);
const PLAIN = topic("t-plain", "Болталка", false);
const EVERY = [RULES, NEWS, PLAIN];

/**
 * The pinned order the fake server keeps, changed by a reorder and a pin it
 * accepts: a topic pinned stands last in it, a topic unpinned leaves it.
 */
let pinnedOnServer = [RULES, NEWS];

/** The topics of the board the fake server keeps out of the pinned block. */
const unpinnedOnServer = () =>
  EVERY.filter((one) => !pinnedOnServer.some((p) => p.id === one.id)).map(
    (one) => ({ ...one, isAttached: false }),
  );

const messages = () => useToast().toasts.value.map((t) => t.message);

let mounted: VueWrapper | null = null;

afterEach(() => {
  mounted?.unmount();
  mounted = null;
});

/**
 * `stubs` replaces the named stubs below for one test, `held` the board the
 * store holds.
 */
async function mountList(
  stubs: Record<string, unknown> = {},
  held: object = board,
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  // Set before mount: the listing's immediate watcher fetches for the board
  // the store already holds.
  useBoardsStore().selectedBoard = held as never;

  const wrapper = mount(TopicsList, {
    shallow: true,
    // In the document, so the caret the arrows keep can be read off it.
    attachTo: document.body,
    global: {
      plugins: [pinia],
      stubs: {
        RouterLink: true,
        // The stubs keep the slots the assertions reach through: a table of
        // real rows, since a drag measures the rows of the pinned block.
        // The words of a tooltip ride on its box, for the assertions to read.
        Tooltip: {
          props: ["text"],
          template: `<div class="tip" :data-tip="text"><slot /></div>`,
        },
        DataTable: {
          props: ["data", "columns", "detailsFor"],
          template: `<table><tbody><template v-for="(row, index) in data" :key="row.id"><tr class="row" :data-id="row.id"><td v-for="column in columns" :key="column.key" :class="'col-' + column.key"><slot :name="'cell-' + column.key" :row="row" :index="index" /></td></tr><tr v-if="detailsFor !== null && detailsFor === row.id" class="details" :data-details-for="row.id"><td><slot name="details" :row="row" :index="index" /></td></tr></template></tbody></table>`,
        },
        // The refusal line, drawn for real: the assertions read its words.
        ActionResult: false,
        Button: {
          props: ["loading", "disabled"],
          template: `<button :disabled="loading || disabled"><slot /></button>`,
        },
        // The strip that stands where the rows would be. Rendered rather than
        // auto-stubbed because the assertions below are about the sentence it
        // is handed, not about its presence.
        ErrorState: {
          props: ["message"],
          template: `<div class="error-strip">{{ message }}</div>`,
        },
        ...stubs,
      },
    },
  });
  mounted = wrapper;
  await flushPromises();
  return wrapper;
}

/** The ids of the listing's rows, top to bottom. */
const rowIds = (wrapper: VueWrapper) =>
  wrapper.findAll("tr.row").map((row) => row.attributes("data-id"));

/**
 * A pointer event as the browser sends it for the primary button; jsdom has
 * no PointerEvent of its own, and the page reads nothing a mouse event lacks.
 */
function pointer(target: Element, type: string, clientY: number) {
  target.dispatchEvent(
    new MouseEvent(type, {
      bubbles: true,
      cancelable: true,
      clientY,
      button: 0,
    }),
  );
}

/**
 * Lays the rows out 40px apart, top to bottom: jsdom lays every box out at
 * zero, and a drag finds its place by the middles of the rows.
 */
function layRows(wrapper: VueWrapper) {
  wrapper.findAll("tr.row").forEach((row, index) => {
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

/** The link of a title drawn for real, so what stands inside it is reached. */
const LINK = {
  RouterLink: { props: ["to"], template: `<a :href="to"><slot /></a>` },
};

describe("TopicsList moderator actions", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));

    localStorage.clear();
    // A board moderator: the actions column and the order of the pinned
    // topics are his.
    localStorage.setItem(
      "user",
      JSON.stringify({ username: "moder", role: "RegularUser" }),
    );

    vi.clearAllMocks();
    pinnedOnServer = [RULES, NEWS];
    mockGetTopics.mockImplementation(
      (_alias: string, query?: { isAttached?: boolean }) =>
        Promise.resolve(
          query?.isAttached
            ? {
                data: { resources: [...pinnedOnServer], paging: null },
                error: null,
              }
            : {
                data: {
                  resources: unpinnedOnServer(),
                  paging: { current: 1, skip: 0, take: 20, total: 1, pages: 1 },
                },
                error: null,
              },
        ),
    );
    mockUpdateTopic.mockImplementation(
      (id: string, patch: { isAttached?: boolean }) => {
        const one = EVERY.find((each) => each.id === id)!;
        pinnedOnServer = pinnedOnServer.filter((each) => each.id !== id);
        if (patch.isAttached) {
          pinnedOnServer = [...pinnedOnServer, { ...one, isAttached: true }];
        }
        return Promise.resolve({ data: null, error: null });
      },
    );
    mockReorderPinnedTopics.mockImplementation(
      (_alias: string, ids: string[]) => {
        pinnedOnServer = ids.map(
          (id) => pinnedOnServer.find((one) => one.id === id)!,
        );
        return Promise.resolve({ data: null, error: null });
      },
    );
  });

  /** The pin before the title of a topic, by the topic's id. */
  const pinOf = (wrapper: VueWrapper, id: string) =>
    wrapper.get(`[data-pin-toggle="${id}"]`);

  /** A click as the browser sends it for a press of the mouse. */
  const click = (target: Element) =>
    target.dispatchEvent(
      new MouseEvent("click", { bubbles: true, cancelable: true, detail: 1 }),
    );

  // The refusal stands in a line opened under the row whose pin was pressed,
  // not in a plate in the corner (the owner's rule of 26 September 2026).
  it("names the pin action the server refused under its row, in the direction it was going", async () => {
    mockUpdateTopic.mockResolvedValue({ data: null, error: refusal(409) });
    const wrapper = await mountList();
    const refusalUnder = () =>
      wrapper
        .findAll("tr.details")
        .map((row) => [
          row.attributes("data-details-for"),
          row.get(".pin-refusal").text(),
        ]);

    // The two pinned rows lead, the regular one follows.
    await wrapper.findAll(".pin-toggle")[0].trigger("click");
    await flushPromises();
    expect(refusalUnder()).toEqual([["t-rules", "Не удалось открепить топик"]]);

    await wrapper.findAll(".pin-toggle")[2].trigger("click");
    await flushPromises();
    expect(refusalUnder()).toEqual([["t-plain", "Не удалось закрепить топик"]]);
    expect(messages()).toEqual([]);
  });

  // Every title has its pin, a toggle named by its action with its state in
  // aria-pressed, as the like is: pale on a topic that is not pinned, and on a
  // pinned one the handle of the order as well. The column of buttons past the
  // dates is gone, and nothing else is added: no button over the table, no
  // mode, no arrows, no window.
  it("puts a pin before every title in place of the column of buttons", async () => {
    const wrapper = await mountList(LINK);

    expect(
      wrapper.findAll("tr.row").map((row) => {
        const pin = row.get(".col-title button.pin-toggle");
        return [
          row.attributes("data-id"),
          pin.attributes("aria-label"),
          pin.attributes("aria-pressed"),
          pin.classes("pinned"),
          pin.attributes("data-pin-handle") ?? null,
          row.get(".col-title .tip").attributes("data-tip"),
        ];
      }),
    ).toEqual([
      [
        "t-rules",
        "Закрепить топик Правила",
        "true",
        true,
        "t-rules",
        "Открепить топик. Перетащите, чтобы изменить порядок",
      ],
      [
        "t-news",
        "Закрепить топик Объявления",
        "true",
        true,
        "t-news",
        "Открепить топик. Перетащите, чтобы изменить порядок",
      ],
      [
        "t-plain",
        "Закрепить топик Болталка",
        "false",
        false,
        null,
        "Закрепить топик",
      ],
    ]);
    // The pin is the mark: the link of a title holds no second one.
    for (const row of wrapper.findAll("tr.row")) {
      expect(row.findAll(".col-title .topic-icon")).toHaveLength(1);
      expect(row.find(".col-title .topic-link .topic-icon").exists()).toBe(
        false,
      );
    }
    // The columns every reader has, and no other.
    expect(
      wrapper
        .findAll("tr.row")[0]
        .findAll("td")
        .map((cell) => cell.classes().find((name) => name.startsWith("col-"))),
    ).toEqual([
      "col-title",
      "col-author",
      "col-comments",
      "col-likes",
      "col-created",
      "col-lastActivity",
    ]);
    expect(wrapper.find(".pin-button").exists()).toBe(false);
    expect(wrapper.find(".manage-pinned-button").exists()).toBe(false);
    expect(wrapper.find(".order-cell").exists()).toBe(false);
    expect(wrapper.find('[role="dialog"]').exists()).toBe(false);
    expect(wrapper.text()).not.toContain("порядок");
  });

  it("pins a topic with a press on its pale pin", async () => {
    const wrapper = await mountList();

    click(pinOf(wrapper, "t-plain").element);
    await flushPromises();

    expect(mockUpdateTopic).toHaveBeenCalledTimes(1);
    expect(mockUpdateTopic).toHaveBeenCalledWith("t-plain", {
      isAttached: true,
    });
    // Read again: the topic stands last in the pinned block, its pin dark and
    // a handle of the order now.
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
    expect(pinOf(wrapper, "t-plain").attributes("aria-pressed")).toBe("true");
    expect(pinOf(wrapper, "t-plain").classes()).toContain("pinned");
    expect(pinOf(wrapper, "t-plain").attributes("data-pin-handle")).toBe(
      "t-plain",
    );
    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();
  });

  it("unpins a topic with a press on its dark pin, a stir of a pixel or two included", async () => {
    const wrapper = await mountList();
    layRows(wrapper);
    const rules = pinOf(wrapper, "t-rules").element;

    // A press as the browser sends it: down, a hand that shakes by two
    // pixels, up, and the click.
    pointer(rules, "pointerdown", 20);
    pointer(rules, "pointermove", 22);
    pointer(rules, "pointerup", 22);
    click(rules);
    await flushPromises();

    expect(mockUpdateTopic).toHaveBeenCalledTimes(1);
    expect(mockUpdateTopic).toHaveBeenCalledWith("t-rules", {
      isAttached: false,
    });
    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();
    // Out of the block, back among the topics of the board.
    expect(rowIds(wrapper)).toEqual(["t-news", "t-rules", "t-plain"]);
    expect(pinOf(wrapper, "t-rules").attributes("aria-pressed")).toBe("false");
    expect(pinOf(wrapper, "t-rules").classes()).not.toContain("pinned");
  });

  // Checked on the stand: a drag of the first pinned topic below the second
  // ends in one click on its pin. Let go where it stood the drag sends no
  // order, and nothing else stood between that click and the unpin.
  it("does not unpin a topic at the end of a drag of its pin", async () => {
    const wrapper = await mountList();
    layRows(wrapper);
    const handle = pinOf(wrapper, "t-rules").element;

    // Held and let go within its own row.
    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 35);
    pointer(handle, "pointerup", 35);
    click(handle);
    await flushPromises();

    // Held, carried under the second, and let go there.
    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 65);
    pointer(handle, "pointerup", 65);
    click(handle);
    await flushPromises();

    expect(mockUpdateTopic).not.toHaveBeenCalled();
    expect(mockReorderPinnedTopics).toHaveBeenCalledTimes(1);
    expect(rowIds(wrapper)).toEqual(["t-news", "t-rules", "t-plain"]);
    expect(pinOf(wrapper, "t-rules").attributes("aria-pressed")).toBe("true");
  });

  it("does not unpin a topic whose drag was taken back with Escape", async () => {
    const wrapper = await mountList();
    layRows(wrapper);
    const handle = pinOf(wrapper, "t-rules");

    pointer(handle.element, "pointerdown", 20);
    pointer(handle.element, "pointermove", 65);
    await handle.trigger("keydown", { key: "Escape" });
    expect(wrapper.find(".held").exists()).toBe(false);
    pointer(handle.element, "pointerup", 65);
    click(handle.element);
    await flushPromises();

    expect(mockUpdateTopic).not.toHaveBeenCalled();
    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
  });

  // The click a drag ends in comes in the task of the release. One that never
  // came must not take the next press with it.
  it("takes the press after a drag for a press", async () => {
    const wrapper = await mountList();
    layRows(wrapper);
    const handle = pinOf(wrapper, "t-rules").element;

    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 35);
    pointer(handle, "pointerup", 35);
    await new Promise((resolve) => setTimeout(resolve));
    // From the keyboard: no pointer before the click.
    await pinOf(wrapper, "t-rules").trigger("click");
    await flushPromises();

    expect(mockUpdateTopic).toHaveBeenCalledWith("t-rules", {
      isAttached: false,
    });
  });

  // The row moves into the pinned block, and focus on a node moved in the page
  // falls to <body>: the caret is put back on the pin it was on.
  it("keeps the caret on a pin pressed from the keyboard", async () => {
    pinnedOnServer = [RULES];
    const OTHER = { ...topic("t-other", "Флуд", false), topicNumber: 3 };
    EVERY.push(OTHER);
    try {
      const wrapper = await mountList();
      expect(rowIds(wrapper)).toEqual([
        "t-rules",
        "t-news",
        "t-plain",
        "t-other",
      ]);

      const pin = pinOf(wrapper, "t-other");
      (pin.element as HTMLElement).focus();
      // A click from the keyboard carries no count of presses.
      pin.element.dispatchEvent(
        new MouseEvent("click", { bubbles: true, cancelable: true, detail: 0 }),
      );
      await flushPromises();

      expect(rowIds(wrapper)).toEqual([
        "t-rules",
        "t-other",
        "t-news",
        "t-plain",
      ]);
      expect(document.activeElement).toBe(pinOf(wrapper, "t-other").element);
    } finally {
      EVERY.pop();
    }
  });

  it("moves a pinned topic where it is let go and sends the order once", async () => {
    const wrapper = await mountList();
    layRows(wrapper);
    const handle = wrapper.findAll("[data-pin-handle]")[0];

    pointer(handle.element, "pointerdown", 20);
    // Below the middle of the second pinned row.
    pointer(handle.element, "pointermove", 65);
    await flushPromises();

    // Held: the row stays where it was, faded, and a line stands under the
    // last pinned row, where the topic would go.
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
    expect(handle.classes()).toContain("held");
    expect(
      wrapper
        .findAll("[data-pin-handle]")
        .map((one) => one.classes().filter((name) => name.startsWith("drop-"))),
    ).toEqual([[], ["drop-after"]]);
    // One line: the first row under the block, a pin too, draws none of its own.
    expect(wrapper.findAll('[class*="drop-"]')).toHaveLength(1);
    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();

    pointer(handle.element, "pointerup", 65);
    await flushPromises();

    expect(rowIds(wrapper)).toEqual(["t-news", "t-rules", "t-plain"]);
    expect(mockReorderPinnedTopics).toHaveBeenCalledTimes(1);
    expect(mockReorderPinnedTopics).toHaveBeenCalledWith("main", [
      "t-news",
      "t-rules",
    ]);
    expect(wrapper.find(".held").exists()).toBe(false);
    expect(wrapper.find('[class*="drop-"]').exists()).toBe(false);
    // The order on screen is the answer: no line, no plate.
    expect(messages()).toEqual([]);
  });

  // The pale pin of a topic that is not pinned moves nothing: the block is the
  // only thing a pin reorders.
  it("drags nothing by the pin of a topic that is not pinned", async () => {
    const wrapper = await mountList();
    layRows(wrapper);
    const plain = pinOf(wrapper, "t-plain").element;

    pointer(plain, "pointerdown", 100);
    pointer(plain, "pointermove", 10);
    await flushPromises();
    expect(wrapper.find('[class*="drop-"]').exists()).toBe(false);
    pointer(plain, "pointerup", 10);
    await flushPromises();

    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
  });

  it("sends nothing for a press that does not travel, or a topic let go where it stood", async () => {
    const wrapper = await mountList();
    layRows(wrapper);
    const handle = wrapper.findAll("[data-pin-handle]")[0].element;

    // A press that stirs by a pixel or two is a press, not a drag: nothing
    // is held.
    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 22);
    await flushPromises();
    expect(wrapper.find(".held").exists()).toBe(false);
    pointer(handle, "pointerup", 22);
    // Dragged within its own row, whose two edges leave the order as it is:
    // held, with no line, since letting it go there changes nothing.
    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 35);
    await flushPromises();
    expect(wrapper.find(".held").exists()).toBe(true);
    expect(wrapper.find('[class*="drop-"]').exists()).toBe(false);
    pointer(handle, "pointerup", 35);
    await flushPromises();

    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
  });

  it("puts the block back in the server's order and says the refusal on a plate", async () => {
    mockReorderPinnedTopics.mockResolvedValue({
      data: null,
      error: {
        ...refusal(409),
        title: "Список закрепленных топиков изменился",
      },
    });
    const wrapper = await mountList();
    layRows(wrapper);
    const handle = wrapper.findAll("[data-pin-handle]")[0].element;

    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 65);
    pointer(handle, "pointerup", 65);
    await flushPromises();

    expect(mockReorderPinnedTopics).toHaveBeenCalledTimes(1);
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
    // No button of this action has a place beside it for a line.
    expect(messages()).toEqual(["Список закрепленных топиков изменился"]);
  });

  it("sends one order at a time, and the last one made waits for the one out", async () => {
    let answer: (value: { data: null; error: null }) => void = () => {};
    mockReorderPinnedTopics.mockImplementationOnce(
      () => new Promise((resolve) => (answer = resolve)),
    );
    const wrapper = await mountList();
    layRows(wrapper);

    // Moved down, then back up while the first save is out.
    await wrapper.findAll("[data-pin-handle]")[0].trigger("keydown", {
      key: "ArrowDown",
    });
    await flushPromises();
    expect(rowIds(wrapper)).toEqual(["t-news", "t-rules", "t-plain"]);
    await wrapper.findAll("[data-pin-handle]")[1].trigger("keydown", {
      key: "ArrowUp",
    });
    await flushPromises();

    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
    expect(mockReorderPinnedTopics).toHaveBeenCalledTimes(1);

    answer({ data: null, error: null });
    await flushPromises();

    expect(mockReorderPinnedTopics.mock.calls.map((call) => call[1])).toEqual([
      ["t-news", "t-rules"],
      ["t-rules", "t-news"],
    ]);
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
  });

  it("moves a pinned topic a place with the arrow keys and keeps the caret on it", async () => {
    const wrapper = await mountList();

    // Up from the top of the block goes nowhere.
    await wrapper.findAll("[data-pin-handle]")[0].trigger("keydown", {
      key: "ArrowUp",
    });
    await flushPromises();
    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();

    const rules = wrapper.findAll("[data-pin-handle]")[0];
    (rules.element as HTMLElement).focus();
    await rules.trigger("keydown", { key: "ArrowDown" });
    await flushPromises();

    expect(rowIds(wrapper)).toEqual(["t-news", "t-rules", "t-plain"]);
    expect(mockReorderPinnedTopics).toHaveBeenCalledTimes(1);
    expect(mockReorderPinnedTopics).toHaveBeenCalledWith("main", [
      "t-news",
      "t-rules",
    ]);
    expect(document.activeElement).toBe(
      wrapper.get('[data-pin-handle="t-rules"]').element,
    );
  });

  it("offers no handle where the pinned block does not stand on its own", async () => {
    const wrapper = await mountList(LINK);

    // A filter mixes the pinned topics into the results, where a pinned one
    // keeps its pin, still the toggle, but moves nowhere.
    const store = useBoardsStore();
    store.attachedTopics = null;
    store.topics = { resources: [RULES, PLAIN], paging: null } as never;
    await flushPromises();

    expect(rowIds(wrapper)).toEqual(["t-rules", "t-plain"]);
    expect(wrapper.find("[data-pin-handle]").exists()).toBe(false);
    expect(
      wrapper.findAll("tr.row")[0].findAll(".col-title .topic-icon"),
    ).toHaveLength(1);
    expect(pinOf(wrapper, "t-rules").attributes("aria-pressed")).toBe("true");
    expect(
      wrapper.findAll("tr.row")[0].get(".tip").attributes("data-tip"),
    ).toBe("Открепить топик");
  });

  it("offers no handle when one topic is pinned", async () => {
    pinnedOnServer = [RULES];
    const wrapper = await mountList(LINK);

    expect(wrapper.find("[data-pin-handle]").exists()).toBe(false);
    expect(
      wrapper
        .findAll("tr.row")
        .map((row) => [
          row.attributes("data-id"),
          row.findAll(".col-title .topic-icon").length,
          row.get(".col-title .pin-toggle").attributes("aria-pressed"),
          row.get(".col-title .tip").attributes("data-tip"),
        ]),
    ).toEqual([
      ["t-rules", 1, "true", "Открепить топик"],
      ["t-news", 1, "false", "Закрепить топик"],
      ["t-plain", 1, "false", "Закрепить топик"],
    ]);
  });
});

/**
 * Who has the pins. The server lets an administrator and the moderators of the
 * board pin a topic (BoardIntentionResolver.AdministratesTopics), and refuses a
 * moderator of the site who does not moderate this board: the pins were his
 * all the same, and every press came back refused.
 */
describe("TopicsList pins, by who may pin", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    pinnedOnServer = [RULES, NEWS];
    mockGetTopics.mockImplementation(
      (_alias: string, query?: { isAttached?: boolean }) =>
        Promise.resolve({
          data: {
            resources: query?.isAttached ? [...pinnedOnServer] : [PLAIN],
            paging: null,
          },
          error: null,
        }),
    );
  });

  const signIn = (username: string, role: string) =>
    localStorage.setItem("user", JSON.stringify({ username, role }));

  it("gives an administrator the pins of a board he does not moderate", async () => {
    signIn("Admin1", "Admin");
    const wrapper = await mountList(LINK);

    expect(wrapper.findAll(".col-title button.pin-toggle")).toHaveLength(3);
    expect(wrapper.findAll("[data-pin-handle]")).toHaveLength(2);
  });

  it.each([
    ["a moderator", "Moderator"],
    ["a senior moderator", "SeniorModerator"],
  ])(
    "shows %s of the site who does not moderate the board the listing everybody reads",
    async (_who, role) => {
      signIn("GlobalMod", role);
      const wrapper = await mountList(LINK);

      expect(wrapper.find(".col-title button").exists()).toBe(false);
      expect(
        wrapper
          .findAll("tr.row")
          .map(
            (row) => row.findAll(".col-title .topic-link .topic-icon").length,
          ),
      ).toEqual([1, 1, 0]);
    },
  );
});

describe("TopicsList pinned topics for a reader", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem(
      "user",
      JSON.stringify({ username: "reader", role: "RegularUser" }),
    );
    vi.clearAllMocks();
    pinnedOnServer = [RULES, NEWS];
    mockGetTopics.mockImplementation(
      (_alias: string, query?: { isAttached?: boolean }) =>
        Promise.resolve({
          data: {
            resources: query?.isAttached ? [...pinnedOnServer] : [PLAIN],
            paging: null,
          },
          error: null,
        }),
    );
  });

  // For anyone who cannot order the block, the pin is the mark it always was,
  // inside the link of the title.
  it("shows the pin as a mark in the title, not as a handle", async () => {
    const wrapper = await mountList(LINK);

    expect(wrapper.find("[data-pin-handle]").exists()).toBe(false);
    expect(wrapper.find(".col-title button").exists()).toBe(false);
    expect(
      wrapper
        .findAll("tr.row")
        .map((row) => row.findAll(".col-title .topic-link .topic-icon").length),
    ).toEqual([1, 1, 0]);
  });
});

/**
 * One refusal, one carrier.
 *
 * The create form has a line of its own above its button and the response
 * interceptor has a plate. For a board policy that refuses the topic, a rate
 * limit, a dead server, a dead connection or an expired session the plate is
 * the carrier, and the line repeating it was one press refused in two places at
 * once. The form still answers for what was typed into it — the field errors of
 * a rejected title or text, and whatever else the server names itself.
 */
describe("TopicsList create form", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));

    localStorage.clear();
    localStorage.setItem(
      "user",
      JSON.stringify({ username: "reader", role: "RegularUser" }),
    );

    vi.clearAllMocks();
    mockGetTopics.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
  });

  /** Opens the inline form and presses "Создать". */
  async function create(wrapper: Awaited<ReturnType<typeof mountList>>) {
    await wrapper.find(".create-topic-button").trigger("click");
    await wrapper.find("form.create-topic-form").trigger("submit");
    await flushPromises();
  }

  // An expired session and a dead connection are about the reader, not the
  // press: they keep the interceptor's plate, and the form says nothing.
  it.each([0, 401])(
    "leaves a failure of status %i to the interceptor",
    async (status) => {
      mockCreateTopic.mockResolvedValue({
        data: null,
        error: { ...refusal(status), title: "Раздел закрыт для записи" },
      });
      const wrapper = await mountList();

      await create(wrapper);

      expect(wrapper.find(".create-topic-error").exists()).toBe(false);
      // Silent about the failure, not about the attempt: nothing was created,
      // so the form stays open instead of folding away as it does on success.
      expect(wrapper.find("form.create-topic-form").exists()).toBe(true);
    },
  );

  // The refusals the interceptor used to plate - the board policy, a rate
  // limit, a dead server - stand in the form now, over its buttons, and the
  // plate is taken down (the owner's rule of 26 September 2026).
  it.each([
    [403, "Раздел закрыт для записи"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Раздел закрыт для записи"],
    [503, "Раздел закрыт для записи"],
  ])("says a failure of status %i in the form", async (status, line) => {
    mockCreateTopic.mockResolvedValue({
      data: null,
      error: { ...refusal(status), title: "Раздел закрыт для записи" },
    });
    const wrapper = await mountList();

    await create(wrapper);

    expect(wrapper.get(".create-topic-error").text()).toBe(line);
    expect(wrapper.find("form.create-topic-form").exists()).toBe(true);
    expect(messages()).toEqual([]);
  });

  it("prints a refusal the interceptor says nothing about", async () => {
    mockCreateTopic.mockResolvedValue({
      data: null,
      error: { ...refusal(409), title: "Топик с таким заголовком уже есть" },
    });
    const wrapper = await mountList();

    await create(wrapper);

    expect(wrapper.get(".create-topic-error").text()).toBe(
      "Топик с таким заголовком уже есть",
    );
  });
});

/**
 * "Создать топик" is offered by the board's answer for the viewer, not to every
 * member. The news and the contests take topics from the moderation alone, and
 * a member got the button there, typed a topic and was refused after "Создать".
 * A guest is invited to sign in only where signing in opens a topic to a
 * member: in the news the invitation led to the same refusal.
 */
describe('TopicsList "Создать топик" by the board\'s answer', () => {
  const answering = (
    canCreateTopic: boolean,
    canCreateTopicAfterSignIn = false,
  ) => ({
    ...board,
    viewer: { canCreateTopic, canCreateTopicAfterSignIn },
  });

  function signIn(username: string | null) {
    localStorage.clear();
    if (username) {
      localStorage.setItem(
        "user",
        JSON.stringify({ username, role: "RegularUser" }),
      );
    }
  }

  beforeEach(() => {
    vi.clearAllMocks();
    mockGetTopics.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
  });

  it("offers the button to a member the board takes topics from", async () => {
    signIn("reader");
    const wrapper = await mountList({}, answering(true));

    expect(wrapper.find(".create-topic-button").exists()).toBe(true);
    expect(wrapper.find("login-prompt-stub").exists()).toBe(false);
  });

  it("offers a member nothing on a board of the moderation", async () => {
    signIn("reader");
    const wrapper = await mountList({}, answering(false));

    expect(wrapper.find(".create-topic-section").exists()).toBe(false);
    expect(wrapper.find(".create-topic-button").exists()).toBe(false);
    expect(wrapper.find("form.create-topic-form").exists()).toBe(false);
  });

  // The board of the list of boards stands in first and carries no answer:
  // nothing is offered on an answer nobody gave.
  it("offers nothing until the board answers", async () => {
    signIn("reader");
    const { viewer: _unanswered, ...listed } = board;
    const wrapper = await mountList({}, listed);

    expect(wrapper.find(".create-topic-section").exists()).toBe(false);
  });

  it("invites a guest to sign in where a member may open a topic", async () => {
    signIn(null);
    const wrapper = await mountList({}, answering(false, true));

    expect(wrapper.find("login-prompt-stub").exists()).toBe(true);
    expect(wrapper.find(".create-topic-button").exists()).toBe(false);
  });

  it("invites a guest to nothing in the news", async () => {
    signIn(null);
    const wrapper = await mountList({}, answering(false, false));

    expect(wrapper.find(".create-topic-section").exists()).toBe(false);
  });

  // The answer is for whoever held the session at the read. Signing in from
  // the invitation reads the board again, and the button follows that answer.
  it("reads the board again for the viewer who signed in", async () => {
    signIn(null);
    mockGetBoard.mockResolvedValue({
      data: { resource: answering(true) },
      error: null,
    });
    const wrapper = await mountList({}, answering(false, true));
    expect(wrapper.find("login-prompt-stub").exists()).toBe(true);

    useAuthStore().updateUser({
      username: "reader",
      role: "RegularUser",
    } as never);
    await flushPromises();

    // With no refusal flags: this read shows nothing of a failure, so the
    // interceptor speaks for it (BOARD_PAGE_OWNS_REFUSAL in the store).
    expect(mockGetBoard).toHaveBeenCalledWith("main", undefined);
    expect(wrapper.find("login-prompt-stub").exists()).toBe(false);
    expect(wrapper.find(".create-topic-button").exists()).toBe(true);
  });
});

/**
 * One refusal, one carrier — the listing's half of it.
 *
 * A board that would not load answered twice: this page stands a strip where
 * the rows would be, and the response interceptor raised its plate over it. The
 * strip is the box in the place the reader is looking, so the reads behind it
 * take the refusal over; and from there the strip owes him the whole sentence,
 * because the plate that used to carry it is silent.
 *
 * An expired session and a dead connection stay with the interceptor: neither
 * is about this board, and only the interceptor can offer the other address.
 */
describe("TopicsList refusal strip", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));

    localStorage.clear();
    localStorage.setItem(
      "user",
      JSON.stringify({ username: "reader", role: "RegularUser" }),
    );

    vi.clearAllMocks();
  });

  it("stands the server's own words where the rows would be", async () => {
    mockGetTopics.mockResolvedValue({
      data: null,
      error: { ...refusal(403), title: "Раздел закрыт для чтения" },
    });

    const wrapper = await mountList();

    expect(wrapper.get(".error-strip").text()).toBe("Раздел закрыт для чтения");
  });

  it("counts out the wait a rate limit asked for", async () => {
    mockGetTopics.mockResolvedValue({
      data: null,
      error: {
        ...refusal(429),
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      },
    });

    const wrapper = await mountList();

    // The seconds are the part a reader acts on, and this strip is now the
    // only place they can appear.
    expect(wrapper.get(".error-strip").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  it("asks for the listing as the one carrier of its own refusal", async () => {
    mockGetTopics.mockResolvedValue({ data: null, error: refusal(500) });

    await mountList();

    // Two reads fill this listing, and the plate has to stay down over both.
    expect(mockGetTopics.mock.calls.map((call) => call[2])).toEqual([
      { ownsRefusal: true, ownsRateLimit: true },
      { ownsRefusal: true, ownsRateLimit: true },
    ]);
  });
});

/**
 * The "Последняя активность" column of a topic nobody has answered.
 *
 * It printed the missing-value token, in English, although the value is not
 * missing: the server sorts the column by the last comment or, without one, by
 * the moment the topic was opened, and the forum index already names a new
 * topic as its board's last activity. The cell says that moment, linked to the
 * topic.
 *
 * And it says who: the name stands before the date, as on the forum index and
 * on DM2 (owner, 5 October 2026). Under the pointer only, it could not be read
 * on a touch screen, where a tap on the date follows the link.
 */
describe("TopicsList last activity", () => {
  const QUIET = topic("t-quiet", "Никто не ответил", false);
  const GONE = {
    ...topic("t-gone", "Ответил удаленный", false),
    topicNumber: 4,
    commentsCount: 1,
    lastComment: {
      id: "c-2",
      createdUtc: "2026-07-06T12:00:00Z",
      author: null,
    },
  };
  const ANSWERED = {
    ...topic("t-answered", "Уже ответили", false),
    topicNumber: 3,
    commentsCount: 1,
    lastComment: {
      id: "c-1",
      createdUtc: "2026-07-05T12:00:00Z",
      author: { username: "reader", role: "RegularUser" },
    },
  };

  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    mockGetTopics.mockImplementation(
      (_alias: string, query?: { isAttached?: boolean }) =>
        Promise.resolve(
          query?.isAttached
            ? { data: { resources: [], paging: null }, error: null }
            : {
                data: {
                  resources: [QUIET, ANSWERED, GONE],
                  paging: { current: 1, skip: 0, take: 20, total: 3, pages: 1 },
                },
                error: null,
              },
        ),
    );
  });

  const cell = (wrapper: VueWrapper, id: string) =>
    wrapper.get(`tr.row[data-id="${id}"] td.col-lastActivity`);

  it("dates a topic nobody has answered by its opening, linked to the topic", async () => {
    const wrapper = await mountList(LINK);

    const quiet = cell(wrapper, "t-quiet");
    expect(quiet.text()).not.toContain(VALUE_UNAVAILABLE);
    expect(quiet.get("human-date-stub").attributes("date")).toBe(
      QUIET.createdUtc,
    );
    expect(quiet.get("a").attributes("href")).toBe("/forum/main/2");
  });

  it("keeps dating an answered topic by its last comment", async () => {
    const wrapper = await mountList(LINK);

    const answered = cell(wrapper, "t-answered");
    expect(answered.get("human-date-stub").attributes("date")).toBe(
      ANSWERED.lastComment.createdUtc,
    );
    expect(answered.get("a").attributes("href")).toBe(
      "/forum/main/3#comment-c-1",
    );
  });

  /** The user the cell names, and what stands between the name and the date. */
  const named = (wrapper: VueWrapper, id: string) => {
    const one = cell(wrapper, id);
    return {
      user: one.findComponent({ name: "UserLink" }),
      removed: one.findComponent({ name: "RemovedAccountLink" }).exists(),
      // The name, the comma, then the date: the order of the markup.
      order: one
        .html()
        .replace(/\s+/g, " ")
        .replace(/<!--.*?-->/g, "")
        .replace(
          /<(user-link|removed-account-link)-stub[^>]*>.*?<\/\1-stub>/,
          "NAME",
        )
        .replace(/<a[^>]*>.*?<\/a>/, "DATE")
        .replace(/<\/?td[^>]*>/g, "")
        .trim(),
      tip: one.find(".tip").exists(),
    };
  };

  it("names who wrote last before the date of an answered topic", async () => {
    const wrapper = await mountList(LINK);

    const answered = named(wrapper, "t-answered");
    expect(answered.user.props("user")).toEqual(ANSWERED.lastComment.author);
    expect(answered.user.props("hideBadge")).toBe(true);
    expect(answered.order).toBe("NAME, DATE");
    expect(answered.tip).toBe(false);
  });

  it("names the author of a topic nobody has answered", async () => {
    const wrapper = await mountList(LINK);

    const quiet = named(wrapper, "t-quiet");
    expect(quiet.user.props("user")).toEqual(QUIET.author);
    expect(quiet.order).toBe("NAME, DATE");
    expect(quiet.tip).toBe(false);
  });

  it("says a removed account where the last one to write is gone", async () => {
    const wrapper = await mountList(LINK);

    const gone = named(wrapper, "t-gone");
    expect(gone.user.exists()).toBe(false);
    expect(gone.removed).toBe(true);
    expect(gone.order).toBe("NAME, DATE");
  });
});

/**
 * Every column of the listing stands in a box narrower than its words, the one
 * it is sorted by among them, and the header of that one announces the sort
 * (aria-sort). The table was not told its sort, and the sort button said
 * "Последняя активность" over a table that had dropped that column.
 *
 * jsdom lays nothing out: the box is answered by `ROOM`, a header by the least
 * width of its column, both as a phone of 360 read them off the seed of
 * /forum/general, so a table that dropped columns to fit would drop them here.
 */
describe("TopicsList sorted column in a narrow box", () => {
  /** The frame of the table on a phone of 360. */
  const ROOM = 296;
  /** Least widths of the columns, their words whole, at 360. */
  const LEAST: Record<string, number> = {
    title: 154.2,
    author: 131.6,
    comments: 120.7,
    likes: 67.2,
    created: 100.6,
    lastActivity: 133.8,
  };
  const EVERY = [
    "Топик",
    "Автор",
    "Комментарии",
    "Лайки",
    "Дата создания",
    "Последняя активность",
  ];
  const realRect = Element.prototype.getBoundingClientRect;

  /** The key of a header of the table ("col-likes"), or null. */
  const keyOf = (cell: Element) =>
    [...cell.classList]
      .find((one) => one.startsWith("col-"))
      ?.slice("col-".length) ?? null;

  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
    mockGetTopics.mockImplementation(
      (_alias: string, query?: { isAttached?: boolean }) =>
        Promise.resolve(
          query?.isAttached
            ? { data: { resources: [], paging: null }, error: null }
            : {
                data: {
                  resources: [PLAIN],
                  paging: { current: 1, skip: 0, take: 20, total: 1, pages: 1 },
                },
                error: null,
              },
        ),
    );
    vi.stubGlobal(
      "ResizeObserver",
      vi.fn(function () {
        return { observe: vi.fn(), unobserve: vi.fn(), disconnect: vi.fn() };
      }),
    );
    Element.prototype.getBoundingClientRect = function (this: Element) {
      const key = keyOf(this);
      const width = this.classList.contains("data-table-scroll")
        ? ROOM
        : this.tagName === "TH" && key
          ? (LEAST[key] ?? 0)
          : 0;
      return {
        width,
        height: 0,
        top: 0,
        left: 0,
        right: width,
        bottom: 0,
      } as DOMRect;
    };
  });

  afterEach(() => {
    Element.prototype.getBoundingClientRect = realRect;
    vi.unstubAllGlobals();
    for (const key of Object.keys(routeQuery))
      Reflect.deleteProperty(routeQuery, key);
  });

  /** The listing, its table drawn for real, sorted as `query` says. */
  async function sortedBy(query: Record<string, string> = {}) {
    Object.assign(routeQuery, query);
    return mountList({ DataTable: false });
  }

  /** The labels of the headers of the table. */
  const standing = (wrapper: VueWrapper) =>
    wrapper.findAll("thead th").map((th) => th.text());

  it("stands every column, sorted by the last activity by default", async () => {
    const wrapper = await sortedBy();

    expect(standing(wrapper)).toEqual(EVERY);
    expect(
      wrapper.get("thead th.col-lastActivity").attributes("aria-sort"),
    ).toBe("descending");
  });

  it("stands every column sorted by the date of creation", async () => {
    const wrapper = await sortedBy({ sortBy: "created", sortOrder: "asc" });

    expect(standing(wrapper)).toEqual(EVERY);
    expect(wrapper.get("thead th.col-created").attributes("aria-sort")).toBe(
      "ascending",
    );
  });

  it("stands every column sorted by the likes", async () => {
    const wrapper = await sortedBy({ sortBy: "likes" });

    expect(standing(wrapper)).toEqual(EVERY);
    expect(wrapper.get("thead th.col-likes").attributes("aria-sort")).toBe(
      "descending",
    );
  });
});
