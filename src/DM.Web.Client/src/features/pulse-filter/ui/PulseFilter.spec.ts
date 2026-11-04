/**
 * @vitest-environment jsdom
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
