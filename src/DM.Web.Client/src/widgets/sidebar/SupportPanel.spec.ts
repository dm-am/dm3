/**
 * @vitest-environment jsdom
 */

// The fundraising in the right column: the bar is always there, as on the help page.
import { describe, it, expect, vi } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { fundraisingApi } from "@/entities/fundraising";
import SupportPanel from "./SupportPanel.vue";

vi.mock("@/entities/fundraising", () => ({
  fundraisingApi: {
    getFundraising: vi.fn(() => new Promise(() => {})),
  },
}));

/** The goal as the server answers it. */
const answering = (goal: { title: string; goalAmount: number }) =>
  vi.mocked(fundraisingApi.getFundraising).mockResolvedValueOnce({
    data: {
      resource: { ...goal, collectedAmount: 0, modifiedUtc: "2026-11-03" },
    },
    error: null,
  } as never);

async function render() {
  setActivePinia(createPinia());
  const wrapper = mount(SupportPanel, {
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  await flushPromises();
  return wrapper;
}

describe("the support panel", () => {
  it("keeps the bar while nothing is announced", async () => {
    answering({ title: "", goalAmount: 0 });
    const wrapper = await render();

    expect(wrapper.get(".progress").text()).toMatch(/^0\s\/\s0\sр\.$/);
    expect(wrapper.text()).toContain("Помогите нам");
  });

  it("names a declared goal over its sum", async () => {
    answering({ title: "Сервер на год", goalAmount: 12000 });
    const wrapper = await render();

    expect(wrapper.get(".goal-title").text()).toBe("Сервер на год");
    expect(wrapper.text()).toContain("0 / 12");
  });
});
