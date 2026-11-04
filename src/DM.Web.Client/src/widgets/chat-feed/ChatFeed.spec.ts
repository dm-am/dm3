/**
 * @vitest-environment jsdom
 */

/** The one feed both chats are drawn with. */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import {
  flushPromises,
  mount,
  RouterLinkStub,
  type VueWrapper,
} from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useToast } from "@/shared/lib/composables/useToast";
import { useAuthStore } from "@/shared/stores";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { ChatMessage } from "@/widgets/chat-message/@x/chat-feed";
import { deferred } from "@/shared/lib/testing/tasks";
import ChatFeed from "./ChatFeed.vue";
import type { ChatFeedActions } from "./types";

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

vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeComposer(
    clearSpy,
  ),
);

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
      hasMoreAfter: false,
      loadOlder: vi.fn(ok),
      loadNewer: vi.fn(ok),
      retry: vi.fn(),
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

const twoOnScreenWith = (
  feedActions: ChatFeedActions,
  props: Record<string, unknown> = {},
) => {
  // Index 0 is the date of the two.
  virtual.rows.value = [
    { index: 0, start: 0, key: "sep", size: 18 },
    { index: 1, start: 18, key: "m1", size: 80 },
    { index: 2, start: 98, key: "m2", size: 80 },
  ];
  return open({
    messages: [message("m1"), message("m2")],
    actions: feedActions,
    ...props,
  });
};

const twoOnScreen = () => twoOnScreenWith(actions());

describe("ChatFeed, a removed message", () => {
  const removedOnScreen = (props: Record<string, unknown>) => {
    virtual.rows.value = [
      { index: 0, start: 0, key: "sep", size: 18 },
      { index: 1, start: 18, key: "m1", size: 80 },
    ];
    return open({
      messages: [
        { ...message("m1"), text: "Удаленный текст", isRemoved: true },
      ],
      ...props,
    });
  };

  beforeEach(() => {
    useAuthStore().updateUser({
      username: "Alice",
      role: "Moderator",
    } as never);
  });

  it("opens to a moderator where moderation reaches, the global chat", async () => {
    const wrapper = removedOnScreen({ moderation: { canWarn: () => false } });

    await wrapper.get("button.msg-deleted").trigger("click");

    expect(wrapper.text()).toContain("Удаленный текст");
  });

  // Rooms and private correspondence are not moderated: the text was never his to read there.
  it("stays shut to a moderator in a feed moderation does not reach", () => {
    const wrapper = removedOnScreen({});

    expect(wrapper.find("button.msg-deleted").exists()).toBe(false);
    expect(wrapper.text()).toContain("Сообщение удалено");
    expect(wrapper.text()).not.toContain("Удаленный текст");
  });
});

describe("ChatFeed, a message on screen", () => {
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

  it("says Нравится in the toolbar, with no tooltip while nobody likes it", async () => {
    useAuthStore().updateUser({ username: "Alice" } as never);
    const wrapper = twoOnScreen();

    await wrapper.get("#msg-m1").trigger("mouseenter");
    await nextTick();

    const like = wrapper.get(".msg-toolbar button.toolbar-btn-text");
    expect(like.text()).toBe("Нравится");
    expect(like.attributes("aria-pressed")).toBe("false");
    expect(like.element.parentElement?.getAttribute("data-tooltip")).toBe("");
  });

  it("lists the likers on the toolbar's Нравится and presses it for the reader's own", async () => {
    useAuthStore().updateUser({ username: "Alice" } as never);
    const liked = {
      ...message("m1"),
      likes: [{ username: "Alice" }, { username: "Carol" }],
    };
    virtual.rows.value = [
      { index: 0, start: 0, key: "sep", size: 18 },
      { index: 1, start: 18, key: "m1", size: 80 },
    ];
    const wrapper = open({ messages: [liked], actions: actions() });

    await wrapper.get("#msg-m1").trigger("mouseenter");
    await nextTick();

    const like = wrapper.get(".msg-toolbar button.toolbar-btn-text");
    expect(like.attributes("aria-pressed")).toBe("true");
    expect(like.classes()).toContain("active");
    expect(like.element.parentElement?.getAttribute("data-tooltip")).toBe(
      "Нравится: Alice, Carol",
    );
  });

  /** An ordinary link to the page standing on the message. */
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
    const target = wrapper
      .findAllComponents(RouterLinkStub)
      .find((stub) => stub.element === link.element);
    expect(target?.props("to")).toEqual({
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

/** A fixed layer takes the nearest transformed or contained ancestor for its box. */
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

  // The stylesheet above does not see the inline style a row is placed with.
  it("place a row by its top, not by a transform", () => {
    const rows = twoOnScreen().findAll(".chat-feed__row");

    expect(rows).toHaveLength(3);
    for (const row of rows) {
      expect(row.attributes("style")).toContain("top:");
      expect(row.attributes("style")).not.toContain("transform");
    }
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

/** Each press on a message takes the line of its last refusal down before it asks the server. */
describe("ChatFeed, the line under a message on the next press", () => {
  const refused = () =>
    Promise.resolve({
      error: { type: "", title: "Нельзя", status: 400, traceId: "t" },
    });

  it("is taken down when the like is pressed again, not when it is answered", async () => {
    useAuthStore().updateUser({ username: "Alice" } as never);
    const answer = deferred<{ error: null }>();
    const like = vi.fn().mockImplementationOnce(refused);
    like.mockImplementationOnce(() => answer.promise);
    const wrapper = twoOnScreenWith(actions({ like }));

    await wrapper.get("#msg-m1").trigger("mouseenter");
    await nextTick();
    const press = () =>
      wrapper.get(".msg-toolbar button.toolbar-btn-text").trigger("click");

    await press();
    await flushPromises();
    expect(wrapper.get(".action-result").text()).toBe("Нельзя");

    await press();
    expect(like).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".action-result").exists()).toBe(false);

    answer.resolve({ error: null });
    await flushPromises();
  });

  it("is taken down when the edit is saved again, not when it is answered", async () => {
    const answer = deferred<{ error: null }>();
    const saveEdit = vi.fn().mockImplementationOnce(refused);
    saveEdit.mockImplementationOnce(() => answer.promise);
    const wrapper = twoOnScreenWith(actions({ saveEdit }));
    const first = wrapper.findAllComponents(ChatMessage)[0];

    first.vm.$emit("save-edit", "новый текст");
    await flushPromises();
    expect(wrapper.get(".action-result").text()).toBe("Нельзя");

    first.vm.$emit("save-edit", "новый текст");
    await nextTick();
    expect(saveEdit).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".action-result").exists()).toBe(false);

    answer.resolve({ error: null });
    await flushPromises();
  });
});

/** The edit box opens on the text of the message, for the last press alone. */
describe("ChatFeed, the edit box", () => {
  // A moderator of the global chat edits any message, however old.
  const editable = (editText: ChatFeedActions["editText"]) => {
    useAuthStore().updateUser({
      username: "Alice",
      role: "Moderator",
    } as never);
    return twoOnScreenWith(actions({ editText }), {
      moderation: { canWarn: () => false },
    });
  };

  async function pressEdit(wrapper: VueWrapper, id: string) {
    await wrapper.get(`#msg-${id}`).trigger("mouseenter");
    await nextTick();
    await wrapper
      .get('.msg-toolbar button[aria-label="Редактировать"]')
      .trigger("click");
  }

  const boxText = (wrapper: VueWrapper, id: string) =>
    wrapper.get<HTMLTextAreaElement>(`#msg-${id} .msg-edit .editor-stub`)
      .element.value;

  it("opens once the text of the message is in, with that text", async () => {
    const text = deferred<string>();
    const editText = vi.fn(() => text.promise);
    const wrapper = editable(editText);

    await pressEdit(wrapper, "m1");
    await flushPromises();
    expect(editText).toHaveBeenCalledWith("m1");
    // An empty box takes typing that the text arriving after it overwrites.
    expect(wrapper.find(".msg-edit").exists()).toBe(false);

    text.resolve("[b]m1[/b]");
    await flushPromises();
    expect(boxText(wrapper, "m1")).toBe("[b]m1[/b]");
  });

  it("drops the text of a press another press overtook", async () => {
    const late = deferred<string>();
    const editText = vi
      .fn()
      .mockImplementationOnce(() => late.promise)
      .mockImplementationOnce(() => Promise.resolve("m2"));
    const wrapper = editable(editText);

    await pressEdit(wrapper, "m1");
    await pressEdit(wrapper, "m2");
    await flushPromises();
    late.resolve("m1");
    await flushPromises();

    expect(wrapper.find("#msg-m1 .msg-edit").exists()).toBe(false);
    expect(boxText(wrapper, "m2")).toBe("m2");
  });

  it("drops the text of a press that Отмена came after", async () => {
    const late = deferred<string>();
    const editText = vi
      .fn()
      .mockImplementationOnce(() => Promise.resolve("m1"))
      .mockImplementationOnce(() => late.promise);
    const wrapper = editable(editText);

    await pressEdit(wrapper, "m1");
    await flushPromises();
    await pressEdit(wrapper, "m2");
    await wrapper
      .get("#msg-m1 .msg-edit-btn:not(.msg-edit-save)")
      .trigger("click");
    late.resolve("m2");
    await flushPromises();

    expect(wrapper.find(".msg-edit").exists()).toBe(false);
  });
});
