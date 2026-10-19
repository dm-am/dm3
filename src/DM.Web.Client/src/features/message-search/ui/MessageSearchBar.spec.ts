/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import type { User } from "@/shared/api/models/common";

const { mockSearchMessages } = vi.hoisted(() => ({
  mockSearchMessages: vi.fn(),
}));

vi.mock("../api/messageSearchApi", () => ({
  default: { searchMessages: mockSearchMessages },
}));

import MessageSearchBar from "./MessageSearchBar.vue";

enableAutoUnmount(afterEach);

const SvgIconStub = {
  template: '<span class="svg-icon-stub" :data-name="name" />',
  props: ["name"],
};

const SecondaryTextStub = { template: "<span><slot /></span>" };

const hit = (id: string, text: string) => ({
  id,
  createdUtc: "2026-01-01T10:00:00Z",
  snippet: [{ text, isMatch: false }],
});

const marked = (id: string, before: string, match: string, after: string) => ({
  ...hit(id, before),
  snippet: [
    { text: before, isMatch: false },
    { text: match, isMatch: true },
    { text: after, isMatch: false },
  ],
});

const answer = (...hits: ReturnType<typeof hit>[]) => ({
  data: {
    resources: hits,
    paging: {
      nextCursor: null,
      prevCursor: null,
      hasNext: false,
      hasPrev: false,
    },
  },
  error: null,
});

const mountBar = (props: Record<string, unknown> = {}) =>
  mount(MessageSearchBar, {
    props,
    slots: { default: '<button class="date-stub">Перейти к дате</button>' },
    global: {
      components: { SecondaryText: SecondaryTextStub },
      stubs: { SvgIcon: SvgIconStub },
    },
  });

async function search(wrapper: ReturnType<typeof mountBar>, text: string) {
  const input = wrapper.find("input");
  await input.setValue(text);
  await input.trigger("keydown", { key: "Enter" });
  await flushPromises();
}

describe("MessageSearchBar", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    mockSearchMessages.mockReset();
    mockSearchMessages.mockResolvedValue(answer());
    useAuthStore().user = { username: "kot" } as unknown as User;
  });

  it("is one filter-bar row: the field and the row's buttons together", () => {
    const wrapper = mountBar();
    const row = wrapper.find(".filter-bar");
    expect(row.exists()).toBe(true);
    expect(row.find(".search-container").exists()).toBe(true);
    expect(row.find(".date-stub").exists()).toBe(true);
  });

  it("shows the hits in a dropdown", async () => {
    const wrapper = mountBar();
    mockSearchMessages.mockResolvedValue(
      answer(hit("m1", "первое"), hit("m2", "второе")),
    );
    await search(wrapper, "привет");

    const dropdown = wrapper.find(".search-dropdown");
    expect(dropdown.exists()).toBe(true);
    expect(dropdown.findAll(".result-row")).toHaveLength(2);
  });

  // The search matches by lexeme: "странник" matches "странников".
  it("marks the words the search matched", async () => {
    const wrapper = mountBar();
    mockSearchMessages.mockResolvedValue(
      answer(marked("m1", "мимо шли ", "странников", " к реке")),
    );
    await search(wrapper, "странник");

    const highlighted = wrapper.findAll("mark.search-highlight");
    expect(highlighted).toHaveLength(1);
    expect(highlighted[0].text()).toBe("странников");
    expect(wrapper.find(".result-snippet").text()).toContain("к реке");
  });

  it("prints a preview that looks like markup as text", async () => {
    const wrapper = mountBar();
    mockSearchMessages.mockResolvedValue(answer(hit("m1", "<b>не тег</b>")));
    await search(wrapper, "тег");

    expect(wrapper.find(".result-snippet").text()).toContain("<b>не тег</b>");
    expect(
      wrapper.find(".result-snippet").element.querySelector("b"),
    ).toBeNull();
  });

  it("stays open after a jump, so the next hit is one click away", async () => {
    const wrapper = mountBar();
    mockSearchMessages.mockResolvedValue(
      answer(hit("m1", "первое"), hit("m2", "второе")),
    );
    await search(wrapper, "привет");

    await wrapper.findAll(".result-row")[0].trigger("click");

    expect(wrapper.emitted("jump")).toEqual([["m1"]]);
    expect(wrapper.find(".search-dropdown").exists()).toBe(true);
    expect(wrapper.findAll(".result-row")).toHaveLength(2);
  });

  it("gives a guest the row without the field the endpoint would refuse", () => {
    useAuthStore().user = null;
    const wrapper = mountBar();
    expect(wrapper.find("input").exists()).toBe(false);
    expect(wrapper.find(".date-stub").exists()).toBe(true);
  });

  it("names the global chat as what it searches", () => {
    const input = mountBar().find("input");
    expect(input.attributes("placeholder")).toBe("Поиск по чату");
    expect(input.attributes("aria-label")).toBe("Поиск по глобальному чату");
  });

  it("names the correspondence over a correspondence, and searches it", async () => {
    const wrapper = mountBar({ scope: "dm:c7" });
    const input = wrapper.find("input");
    expect(input.attributes("placeholder")).toBe("Поиск по переписке");
    expect(input.attributes("aria-label")).toBe("Поиск по переписке");

    await search(wrapper, "привет");

    expect(mockSearchMessages.mock.calls[0][0]).toMatchObject({
      in: ["dm:c7"],
    });
  });

  it("starts the next correspondence with an empty field and no hits", async () => {
    const wrapper = mountBar({ scope: "dm:c7" });
    mockSearchMessages.mockResolvedValue(answer(hit("m1", "первое")));
    await search(wrapper, "привет");
    expect(wrapper.find(".search-dropdown").exists()).toBe(true);

    await wrapper.setProps({ scope: "dm:c8" });

    expect((wrapper.find("input").element as HTMLInputElement).value).toBe("");
    expect(wrapper.find(".search-dropdown").exists()).toBe(false);
  });
});
