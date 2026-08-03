/**
 * @vitest-environment jsdom
 */

/**
 * What the chat's search used to be: a full-screen panel inside the chat frame
 * that covered the feed it was searching, with a scope segmented control, a
 * "По дате | Лучшее совпадение" pair of link buttons that copied as three
 * lines, and a source header above every run of hits. Picking a hit closed the
 * whole thing, so reading a second hit meant searching again.
 *
 * What it is now, and what these pin: the site's one search row (a
 * FilterSearchInput plus $control-height buttons on a .filter-bar line, the
 * shape the forum, games, blogs and the rest already use), hits in a dropdown
 * that stays up while the feed jumps under it, no ordering control and no
 * source headers — the source is the global chat, always.
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

const hit = (id: string, snippet: string) => ({
  sourceType: "global" as const,
  sourceId: "c1",
  sourceTitle: null,
  id,
  createdUtc: "2026-01-01T10:00:00Z",
  snippet,
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

const mountBar = () =>
  mount(MessageSearchBar, {
    slots: { default: '<button class="date-stub">Перейти к дате</button>' },
    global: {
      components: { SecondaryText: SecondaryTextStub },
      stubs: { SvgIcon: SvgIconStub },
    },
  });

/** Type a query and apply it at once (Enter), as a reader does. */
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

  it("offers no way to reorder the hits", async () => {
    const wrapper = mountBar();
    mockSearchMessages.mockResolvedValue(answer(hit("m1", "первое")));
    await search(wrapper, "привет");

    expect(wrapper.text()).not.toContain("Лучшее совпадение");
    expect(wrapper.text()).not.toContain("По дате");
    expect(wrapper.find(".sort-control").exists()).toBe(false);
  });

  it("shows the hits in a dropdown, without source headers", async () => {
    const wrapper = mountBar();
    mockSearchMessages.mockResolvedValue(
      answer(hit("m1", "первое"), hit("m2", "второе")),
    );
    await search(wrapper, "привет");

    const dropdown = wrapper.find(".search-dropdown");
    expect(dropdown.exists()).toBe(true);
    expect(dropdown.findAll(".result-row")).toHaveLength(2);
    expect(wrapper.find(".result-group").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Глобальный чат");
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
});
