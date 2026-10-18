/**
 * @vitest-environment jsdom
 */

/**
 * The messenger: the open correspondence and, at its right, the list of
 * conversations at the width the reader dragged it to. The edge between the
 * two is a separator a reader moves with the pointer and the arrows. A list
 * with no rows (none yet, or a failed load) keeps its words, so the sentence
 * it says instead of rows can be read whatever width was left stored.
 */
import { beforeEach, describe, expect, it } from "vitest";
import { defineComponent, h } from "vue";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useMessagingStore } from "@/entities/message";
import MessengerPage from "./MessengerPage.vue";
import {
  LIST_FULL_WIDTH,
  LIST_NARROW_WIDTH,
  LIST_WIDTH_KEY,
} from "./useListWidth";

/** The page of the list the store holds; only its rows count here. */
type ListOfChats = ReturnType<typeof useMessagingStore>["chats"];

const ChatsListStub = defineComponent({
  name: "ChatsList",
  props: { narrow: { type: Boolean, default: false } },
  setup: () => () => h("div", { class: "list-stub" }),
});

function open(rows: number | "failed") {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useMessagingStore();
  if (rows === "failed") {
    store.chats = null;
    store.error = "Не удалось загрузить переписки";
  } else {
    store.chats = {
      resources: Array.from({ length: rows }, (_, i) => ({ id: `c${i}` })),
      paging: { total: rows, pages: 1, current: 1, size: 20, number: 1 },
    } as unknown as ListOfChats;
  }
  return mount(MessengerPage, {
    global: {
      plugins: [pinia],
      stubs: {
        RouterView: { template: "<div class='view-stub' />" },
        PageTitle: { template: "<h1><slot /></h1>" },
        ChatsList: ChatsListStub,
      },
    },
  });
}

const list = (wrapper: ReturnType<typeof open>) =>
  wrapper.get("#messenger-list");
const narrowOf = (wrapper: ReturnType<typeof open>) =>
  wrapper.findComponent(ChatsListStub).props("narrow");

beforeEach(() => localStorage.clear());

describe("MessengerPage", () => {
  it("lays the correspondence and the list side by side, the edge between", () => {
    const wrapper = open(2);

    const parts = [...wrapper.get(".messenger").element.children].map(
      (el) => el.className,
    );
    expect(parts).toEqual([
      "messenger__chat",
      "messenger__edge",
      "messenger__list",
    ]);
    expect(list(wrapper).attributes("style")).toContain(
      `width: ${LIST_FULL_WIDTH}px`,
    );
    const edge = wrapper.get(".messenger__edge");
    expect(edge.attributes("role")).toBe("separator");
    expect(edge.attributes("aria-valuenow")).toBe(String(LIST_FULL_WIDTH));
    expect(edge.attributes("tabindex")).toBe("0");
  });

  it("stands the list narrow at the width the reader left it", () => {
    localStorage.setItem(LIST_WIDTH_KEY, String(LIST_NARROW_WIDTH));
    const wrapper = open(2);

    expect(narrowOf(wrapper)).toBe(true);
    expect(list(wrapper).attributes("style")).toContain(
      `width: ${LIST_NARROW_WIDTH}px`,
    );
  });

  it.each([
    ["no conversation yet", 0],
    ["a failed load", "failed"],
  ] as const)("keeps the words of a list with %s", (_, rows) => {
    localStorage.setItem(LIST_WIDTH_KEY, String(LIST_NARROW_WIDTH));
    const wrapper = open(rows);

    expect(narrowOf(wrapper)).toBe(false);
    expect(list(wrapper).attributes("style")).toContain(
      `width: ${LIST_FULL_WIDTH}px`,
    );
  });

  it("narrows and widens the list from the keyboard", async () => {
    const wrapper = open(2);
    const edge = wrapper.get(".messenger__edge");

    await edge.trigger("keydown", { key: "ArrowRight" });
    expect(edge.attributes("aria-valuenow")).toBe(String(LIST_FULL_WIDTH - 16));
    await edge.trigger("keydown", { key: "ArrowLeft" });
    expect(edge.attributes("aria-valuenow")).toBe(String(LIST_FULL_WIDTH));
  });

  it("gives the list its words back when it asks", async () => {
    localStorage.setItem(LIST_WIDTH_KEY, String(LIST_NARROW_WIDTH));
    const wrapper = open(2);

    wrapper.findComponent(ChatsListStub).vm.$emit("widen");
    await wrapper.vm.$nextTick();

    expect(narrowOf(wrapper)).toBe(false);
  });
});
