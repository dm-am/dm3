/**
 * @vitest-environment jsdom
 */

/**
 * The feed both chats are drawn with.
 *
 * The global chat and a correspondence had a feed each, and the copies parted
 * in a dozen places a reader could see (owner, 5 October 2026: "с какого
 * перепугу личные сообщения визуально отличаются от глобального чата?"). What
 * is held here is what the two now share: the states of the feed, the frame
 * and the line at its top, one fold height, the flash of a message one was
 * taken to, the toolbar's words and link, and a composer that stands only for
 * a reader who may write.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useToast } from "@/shared/lib/composables/useToast";
import { useAuthStore } from "@/shared/stores";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import ChatFeed from "./ChatFeed.vue";
import { MESSAGE_FOLD_HEIGHT, type ChatFeedActions } from "./types";

const virtual = vi.hoisted(() => ({
  rows: null as unknown as { value: unknown[] },
  scrollToIndex: vi.fn(),
}));

/**
 * The virtual list, with the rows a test puts on screen: jsdom lays nothing
 * out, so the real one would draw none.
 */
vi.mock("@/shared/lib/composables/useVirtualScroll", async () => {
  const { ref } = await import("vue");
  virtual.rows = ref([]);
  return {
    useVirtualScroll: () => ({
      virtualizer: ref(null),
      virtualItems: virtual.rows,
      totalSize: ref(0),
      measureElement: vi.fn(),
      scrollToIndex: virtual.scrollToIndex,
      range: ref({ startIndex: 0, endIndex: 0 }),
    }),
  };
});

const { clearSpy } = vi.hoisted(() => ({ clearSpy: vi.fn() }));

/** The editor, as a textarea on the same v-model: tiptap is not the point. */
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: defineComponent({
    name: "BBCodeEditor",
    props: { modelValue: { type: String, default: "" } },
    emits: ["update:modelValue", "submit"],
    setup(props, { emit, expose }) {
      expose({ clear: clearSpy, focus: vi.fn() });
      return () =>
        h("textarea", {
          class: "editor-stub",
          value: props.modelValue,
          onInput: (e: Event) =>
            emit("update:modelValue", (e.target as HTMLTextAreaElement).value),
        });
    },
  }),
}));

/** The message, as the feed hands it over: its props, readable. */
const ChatMessageStub = defineComponent({
  name: "ChatMessage",
  props: {
    message: { type: Object, required: true },
    maxHeight: { type: Number, default: 0 },
    // Read only to show that nothing is handed in for it.
    narrow: { type: Boolean, default: undefined },
  },
  setup: (props) => () =>
    h("div", { class: "message-stub" }, String(props.message.id)),
});

/** A router link that says where it leads, so a test reads the target. */
const RouterLinkStub = defineComponent({
  name: "RouterLink",
  props: { to: { type: [String, Object], required: true } },
  setup(props, { slots, attrs }) {
    return () =>
      h(
        "a",
        { ...attrs, "data-to": JSON.stringify(props.to) },
        slots.default?.(),
      );
  },
});

/** A tooltip that keeps its words on the trigger. */
const TooltipStub = defineComponent({
  props: { text: { type: String, default: "" } },
  setup(props, { slots }) {
    return () =>
      h("span", { "data-tooltip": props.text }, slots.default?.() ?? []);
  },
});

const message = (id: string, author = "Bob") => ({
  id,
  text: id,
  createdUtc: "2026-10-05T10:00:00Z",
  author: { username: author },
  likes: [],
  edits: [],
  isRemoved: false,
});

const ok = () => Promise.resolve({ error: null });

function actions(over: Partial<ChatFeedActions> = {}): ChatFeedActions {
  return {
    send: vi.fn(ok),
    remove: vi.fn(ok),
    like: vi.fn(ok),
    unlike: vi.fn(ok),
    editText: vi.fn(() => Promise.resolve("")),
    saveEdit: vi.fn(ok),
    ...over,
  };
}

const mounted: VueWrapper[] = [];

function open(props: Record<string, unknown> = {}, slots = {}) {
  const wrapper = mount(ChatFeed, {
    props: {
      messages: [],
      loading: false,
      emptyText: "Сообщений пока нет",
      label: "Сообщения",
      context: "message",
      hasMoreBefore: false,
      loadOlder: vi.fn(ok),
      actions: actions(),
      sending: false,
      canWrite: true,
      draftKey: "feed-spec",
      linkTo: (id: string) => ({ name: "chat", query: { msg: id } }),
      ...props,
    },
    slots,
    attachTo: document.body,
    global: {
      stubs: {
        ChatMessage: ChatMessageStub,
        Tooltip: TooltipStub,
        RouterLink: RouterLinkStub,
      },
    },
  });
  mounted.push(wrapper);
  return wrapper;
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  setActivePinia(createPinia());
  virtual.rows.value = [];
  for (const name of ["ResizeObserver", "IntersectionObserver"])
    vi.stubGlobal(
      name,
      class {
        observe() {}
        unobserve() {}
        disconnect() {}
      },
    );
});

afterEach(() => {
  while (mounted.length) mounted.pop()?.unmount();
  vi.useRealTimers();
  document.body.innerHTML = "";
});

describe("ChatFeed, what stands in the feed", () => {
  it("is the skeleton while the window is on its way, and no answer", () => {
    const wrapper = open({ loading: true, error: "Не удалось" });

    expect(wrapper.find(".chat-message-skeleton-list").exists()).toBe(true);
    expect(wrapper.text()).not.toContain("Сообщений пока нет");
    expect(wrapper.find(".error-state").exists()).toBe(false);
  });

  it("is the red box with a retry when the window did not load", async () => {
    const retry = vi.fn();
    const wrapper = open({ error: "Не удалось загрузить сообщения", retry });

    expect(wrapper.get(".error-state").text()).toContain(
      "Не удалось загрузить сообщения",
    );
    expect(wrapper.text()).not.toContain("Сообщений пока нет");
    await wrapper.get(".error-retry").trigger("click");
    expect(retry).toHaveBeenCalled();
  });

  it("is the page's words when the window is empty", () => {
    const wrapper = open({ emptyText: "Сообщений пока нет" });

    expect(wrapper.get(".chat-feed__empty").text()).toBe("Сообщений пока нет");
  });

  it("is the line at the top of the frame, before the list", () => {
    const wrapper = open(
      {},
      { strip: () => h("div", { class: "strip-probe" }, "Идет: Ночной сбор") },
    );

    const frame = wrapper.get(".chat-feed__frame").element;
    expect(frame.firstElementChild?.className).toBe("strip-probe");
  });
});

describe("ChatFeed, a message on screen", () => {
  const twoOnScreen = () => {
    // Index 0 is the date of the two.
    virtual.rows.value = [
      { index: 0, start: 0, key: "sep", size: 18 },
      { index: 1, start: 18, key: "m1", size: 80 },
      { index: 2, start: 98, key: "m2", size: 80 },
    ];
    return open({ messages: [message("m1"), message("m2")] });
  };

  /**
   * A feed narrower than 480 once drew its messages in a narrow form, the
   * avatar 48 instead of 72, so a phone showed smaller pictures than a
   * desktop. The owner, 12 October 2026: the pictures of a phone are the size
   * they are on the full version. The feed draws its messages alike at any
   * width, and has no narrow form to hand them.
   */
  it("draws its messages alike at any width", async () => {
    const reports: ((entries: { contentRect: { width: number } }[]) => void)[] =
      [];
    vi.stubGlobal(
      "ResizeObserver",
      class {
        constructor(callback: (typeof reports)[number]) {
          reports.push(callback);
        }
        observe() {}
        unobserve() {}
        disconnect() {}
      },
    );
    const wrapper = twoOnScreen();
    for (const report of reports) report([{ contentRect: { width: 285 } }]);
    await nextTick();

    expect(
      wrapper.findAllComponents(ChatMessageStub).map((m) => m.props("narrow")),
    ).toEqual([undefined, undefined]);
    expect(wrapper.get(".chat-feed__list").classes()).not.toContain(
      "chat-feed__list--narrow",
    );
    // A row is placed by its top, not by a transform: a transform would make
    // the row the box of every fixed menu inside it.
    expect(wrapper.get(".chat-feed__row").attributes("style")).not.toContain(
      "transform",
    );
  });

  it("folds at the one height of both feeds", () => {
    const wrapper = twoOnScreen();

    const folds = wrapper
      .findAllComponents(ChatMessageStub)
      .map((m) => m.props("maxHeight"));
    expect(folds).toEqual([MESSAGE_FOLD_HEIGHT, MESSAGE_FOLD_HEIGHT]);
    expect(MESSAGE_FOLD_HEIGHT).toBe(500);
  });

  it("keeps the strip's distance above the very start of the history", async () => {
    const wrapper = twoOnScreen();
    const rows = wrapper.findAll(".chat-feed__row");
    expect(rows[0].classes()).toContain("chat-feed__row--start");
    expect(rows[1].classes()).not.toContain("chat-feed__row--start");

    await wrapper.setProps({ hasMoreBefore: true });
    expect(wrapper.find(".chat-feed__row--start").exists()).toBe(false);
  });

  it("marks the arrival of a message it was taken to with the site's pulse", async () => {
    vi.useFakeTimers();
    const wrapper = twoOnScreen();

    (
      wrapper.vm as unknown as { scrollToMessage(id: string): void }
    ).scrollToMessage("m2");
    expect(virtual.scrollToIndex).toHaveBeenCalledWith(2, { align: "center" });
    await nextTick();
    vi.advanceTimersByTime(100);

    expect(document.getElementById("msg-m2")?.classList).toContain(
      "highlight-unread",
    );
  });

  it("names the like the same in both chats", async () => {
    useAuthStore().updateUser({ username: "Alice" } as never);
    const wrapper = twoOnScreen();

    await wrapper.get("#msg-m1").trigger("mouseenter");
    await nextTick();

    const like = wrapper.get(
      '.msg-toolbar button[aria-label="Поставить лайк"]',
    );
    expect(like.element.parentElement?.getAttribute("data-tooltip")).toBe(
      "Поставить лайк",
    );
  });

  /**
   * The link of a message is an ordinary link to the page standing on it:
   * the reader opens it, or copies its address the way he copies any link.
   */
  it("links the message where the page says, and copies nothing", async () => {
    useAuthStore().updateUser({ username: "Alice" } as never);
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText },
      configurable: true,
    });
    const wrapper = twoOnScreen();

    await wrapper.get("#msg-m1").trigger("mouseenter");
    await nextTick();

    const link = wrapper.get(
      '.msg-toolbar a[aria-label="Ссылка на сообщение"]',
    );
    expect(JSON.parse(link.attributes("data-to")!)).toEqual({
      name: "chat",
      query: { msg: "m1" },
    });
    expect(link.element.parentElement?.getAttribute("data-tooltip")).toBe(
      "Ссылка на сообщение",
    );
    await link.trigger("click");
    await flushPromises();
    expect(writeText).not.toHaveBeenCalled();
    expect(useToast().toasts.value).toEqual([]);
  });
});

/**
 * A fixed layer takes the nearest transformed or contained ancestor for its
 * box. The menu "Еще" of a message's header and the tag menu of its edit box
 * are fixed layers, and in a feed whose rows wore a compositing transform they
 * opened inside the clipped list instead of over the page.
 */
describe("ChatFeed, the boxes around a message", () => {
  const BOXES = /\.chat-feed(__(frame|list|row|message|date))?(?![\w-])/;
  const TRAPS = [
    "transform",
    "contain",
    "container-type",
    "will-change",
    "filter",
    "perspective",
  ];

  it("hold no transform and no containment", () => {
    const css = componentCss(
      resolve(dirname(fileURLToPath(import.meta.url)), "ChatFeed.vue"),
    );
    const trapped = styleRules(css)
      .filter(({ selector }) =>
        selector.split(",").some((part) => {
          const subject = part.trim().split(/\s+/).pop() ?? "";
          return BOXES.test(subject);
        }),
      )
      .flatMap(({ selector, declarations }) =>
        TRAPS.filter((prop) => prop in declarations).map(
          (prop) => `${selector}: ${prop}`,
        ),
      );
    expect(trapped).toEqual([]);
  });
});

describe("ChatFeed, the composer", () => {
  it("stands for a reader who may write, and sends through the store", async () => {
    const send = vi.fn(ok);
    const wrapper = open({ actions: actions({ send }) });

    await wrapper.get(".editor-stub").setValue("привет");
    await wrapper.get(".chat-composer__send").trigger("click");
    await flushPromises();

    expect(send).toHaveBeenCalledWith("привет");
    expect(clearSpy).toHaveBeenCalled();
  });

  it("gives the text back and says why when the send is refused", async () => {
    const send = vi.fn(() =>
      Promise.resolve({
        error: { type: "", title: "", status: 400, traceId: "t" },
      }),
    );
    const wrapper = open({ actions: actions({ send }) });

    await wrapper.get(".editor-stub").setValue("привет");
    await wrapper.get(".chat-composer__send").trigger("click");
    await flushPromises();

    expect(
      (wrapper.get(".editor-stub").element as HTMLTextAreaElement).value,
    ).toBe("привет");
    expect(wrapper.get(".chat-composer .action-result").text()).not.toBe("");
    expect(clearSpy).not.toHaveBeenCalled();
  });

  it("is not there for a reader who may not write, and the page's slot is", () => {
    const wrapper = open(
      { canWrite: false },
      { closed: () => h("p", { class: "closed-probe" }, "Войдите") },
    );

    expect(wrapper.find(".chat-composer").exists()).toBe(false);
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
    expect(wrapper.find(".closed-probe").exists()).toBe(true);
  });
});
