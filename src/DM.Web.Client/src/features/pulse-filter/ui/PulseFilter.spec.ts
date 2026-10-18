/**
 * @vitest-environment jsdom
 */

/**
 * The pulse used to word its own bubbles: "от 5 до 20" for a range of ratings
 * and "с ... по ..." for a range of dates, while every other filter on the
 * site wrote both with a dash. The wording now comes from the shared helpers,
 * and these mount the filter on a query that sets both ranges to see what a
 * reader of the page actually gets.
 */
import { describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { createRouter, createMemoryHistory } from "vue-router";
import { createPinia } from "pinia";
import PulseFilter from "./PulseFilter.vue";

vi.mock("@/entities/game", () => ({
  gameApi: { getGame: vi.fn() },
}));

async function mountWithQuery(query: Record<string, string>) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: "/pulse", component: { template: "<div />" } }],
  });
  await router.push({ path: "/pulse", query });
  await router.isReady();

  const wrapper = mount(PulseFilter, {
    global: {
      plugins: [router, createPinia()],
      stubs: { SvgIcon: { template: '<span class="svg-icon-stub" />' } },
    },
  });
  await wrapper.vm.$nextTick();
  return wrapper;
}

function bubbleWithPrefix(wrapper: ReturnType<typeof mount>, prefix: string) {
  const bubble = wrapper
    .findAll(".bubble")
    .find((b) => b.find(".bubble-prefix").text() === prefix);
  expect(bubble, `bubble "${prefix}" is on the row`).toBeTruthy();
  return bubble!.find(".bubble-value-text").text();
}

describe("pulse filter bubbles", () => {
  it("writes a rating range the way every other filter writes one", async () => {
    const wrapper = await mountWithQuery({ minRating: "5", maxRating: "20" });
    expect(bubbleWithPrefix(wrapper, "Рейтинг:")).toBe("5 — 20");
  });

  it("writes a date range the way every other filter writes one", async () => {
    const wrapper = await mountWithQuery({
      createdFrom: "2026-09-01",
      createdTo: "2026-09-20",
    });
    expect(bubbleWithPrefix(wrapper, "Дата создания:")).toBe(
      "01.09.2026 — 20.09.2026",
    );
  });

  it("names the end that is set when a range is open on one side", async () => {
    const open = await mountWithQuery({
      minRating: "5",
      createdTo: "2026-09-20",
    });
    expect(bubbleWithPrefix(open, "Рейтинг:")).toBe("от 5");
    expect(bubbleWithPrefix(open, "Дата создания:")).toBe("до 20.09.2026");
  });
});

/**
 * The panel draws its own frame, and the field block that ends it used to draw
 * a separator on top of that frame — two hairlines reading as one thick line.
 * The rule that drops the last separator is written in the filter's own scoped
 * stylesheet and reaches the block through the scope id the host puts on it,
 * so the block has to be both the last child of the panel and in that scope.
 */
describe("pulse filter panel", () => {
  it("ends its date leaf with a block the filter's own stylesheet reaches", async () => {
    const wrapper = await mountWithQuery({});
    const scopeOf = (el: Element) =>
      [...el.attributes]
        .map((a) => a.name)
        .filter((n) => n.startsWith("data-v-"));
    const host = scopeOf(wrapper.element);

    await wrapper.find(".filter-section button").trigger("click");
    await wrapper.vm.$nextTick();
    const items = wrapper.findAll(".filter-dropdown-container > *");
    await items[items.length - 1].trigger("click");
    await wrapper.vm.$nextTick();

    const panel = wrapper.element.querySelector(".filter-dropdown-container");
    const last = panel?.lastElementChild;
    expect(last?.className).toBe("dropdown-date-range");
    expect(scopeOf(last!)).toEqual(expect.arrayContaining(host));
  });
});
