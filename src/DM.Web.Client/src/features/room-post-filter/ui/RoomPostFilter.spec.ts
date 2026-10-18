/**
 * @vitest-environment jsdom
 */

/**
 * The room bar is the pulse bar with two filters of the room's own. These
 * mount it on a router and go through it the way a reader does: the menu
 * offers the five filters in the order the bubbles stand in, the characters
 * list offers "Без персонажа" and every character that has been in play, with
 * its player (or "NPC"), a picked character stands as a bubble under its name, the sort
 * starts at the room's own order, and "Сбросить" takes the address back to the
 * bare room.
 */
import { describe, it, expect, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createRouter, createMemoryHistory } from "vue-router";
import { createPinia } from "pinia";
import type { Character } from "@/entities/game";
import RoomPostFilter from "./RoomPostFilter.vue";

vi.mock("@/entities/user", () => ({
  UserMultiSelect: { template: '<div class="user-multi-select" />' },
}));

const characters = [
  {
    id: "c-elvira",
    name: "Эльвира Чародейка",
    status: "Active",
    isNpc: false,
    author: { username: "Experienced" },
  },
  {
    id: "c-stranger",
    name: "Таинственный Незнакомец",
    status: "Active",
    isNpc: true,
    author: { username: "TestMentor" },
  },
  {
    id: "c-diopsid",
    name: "Диопсид",
    status: "Retired",
    isNpc: false,
    author: { username: "LongestLoginPossible" },
  },
  // Never in play: nothing was written by them.
  {
    id: "c-liara",
    name: "Лиара Тенебраум",
    status: "UnderReview",
    isNpc: false,
    author: { username: "RatingNegative" },
  },
  {
    id: "c-cassandra",
    name: "Кассандра Видящая",
    status: "Declined",
    isNpc: false,
    author: { username: "Игрок_Один" },
  },
] as unknown as Character[];

async function mountBar(query: Record<string, string> = {}) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/room", name: "room", component: { render: () => null } },
    ],
  });
  await router.push({ path: "/room", query });
  await router.isReady();

  const wrapper = mount(RoomPostFilter, {
    props: { characters },
    attachTo: document.body,
    global: {
      plugins: [router, createPinia()],
      stubs: { SvgIcon: { template: '<span class="svg-icon-stub" />' } },
    },
  });
  await flushPromises();
  return { wrapper, router };
}

// The filter writes the address after a short debounce.
const settle = async () => {
  await new Promise((resolve) => setTimeout(resolve, 80));
  await flushPromises();
};

const labels = (wrapper: Awaited<ReturnType<typeof mountBar>>["wrapper"]) =>
  wrapper.findAll(".dropdown-item .item-label").map((l) => l.text());

describe("room filter bar", () => {
  it("offers the five filters in the order of the bubbles", async () => {
    const { wrapper } = await mountBar();
    await wrapper.find(".filter-btn").trigger("click");
    expect(labels(wrapper)).toEqual([
      "Авторы",
      "Персонажи",
      "Рейтинг",
      "Оценки",
      "Дата создания",
    ]);
    wrapper.unmount();
  });

  it("lists the posts without a character and every character in play with its player", async () => {
    const { wrapper } = await mountBar();
    await wrapper.find(".filter-btn").trigger("click");
    await wrapper.findAll(".dropdown-item")[1].trigger("click");

    expect(labels(wrapper)).toEqual([
      "Без персонажа",
      "Диопсид",
      "Таинственный Незнакомец",
      "Эльвира Чародейка",
    ]);
    const hints = wrapper
      .findAll(".dropdown-item .item-hint")
      .map((h) => h.text());
    expect(hints).toEqual([
      "Мастер и помощники от своего имени",
      "LongestLoginPossible",
      "NPC",
      "Experienced",
    ]);

    await wrapper.find(".dropdown-search-input").setValue("эль");
    expect(labels(wrapper)).toEqual(["Эльвира Чародейка"]);
    wrapper.unmount();
  });

  it("puts a picked character into the address and names it in a bubble", async () => {
    const { wrapper, router } = await mountBar({ q: "при" });
    await wrapper.find(".filter-btn").trigger("click");
    await wrapper.findAll(".dropdown-item")[1].trigger("click");
    const elvira = wrapper
      .findAll(".dropdown-item")
      .find((i) => i.text().includes("Эльвира"));
    await elvira!.trigger("click");
    await settle();

    expect(router.currentRoute.value.query).toEqual({
      q: "при",
      characters: "c-elvira",
    });
    const bubble = wrapper.find(".bubble");
    expect(bubble.find(".bubble-prefix").text()).toBe("Персонаж:");
    expect(bubble.find(".bubble-value-text").text()).toBe("Эльвира Чародейка");
    wrapper.unmount();
  });

  it("names the reviews choice in its bubble", async () => {
    const { wrapper } = await mountBar({ reviews: "without" });
    const bubble = wrapper.find(".bubble");
    expect(bubble.find(".bubble-prefix").text()).toBe("Оценки:");
    expect(bubble.find(".bubble-value-text").text()).toBe("нет");
    wrapper.unmount();
  });

  it("starts the sort at the room's own order", async () => {
    const { wrapper } = await mountBar();
    expect(wrapper.find(".sort-btn").text()).toBe("По порядку");
    await wrapper.find(".sort-btn").trigger("click");
    expect(wrapper.findAll(".sort-option-label").map((l) => l.text())).toEqual([
      "По порядку",
      "По рейтингу",
      "По количеству оценок",
      "Последние оцененные",
    ]);
    wrapper.unmount();
  });

  it("takes the address back to the bare room on reset", async () => {
    const { wrapper, router } = await mountBar({
      q: "при",
      characters: "none,c-diopsid",
      reviews: "with",
      sort: "rating",
      order: "desc",
    });
    await wrapper.find(".clear-all-link").trigger("click");
    await settle();

    expect(router.currentRoute.value.query).toEqual({});
    expect(wrapper.find(".bubble").exists()).toBe(false);
    wrapper.unmount();
  });
});
