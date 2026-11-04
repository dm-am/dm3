/**
 * @vitest-environment jsdom
 */

// A found fault goes to the forum's "Ошибки", as the support page says, not to the administration's form.
import { describe, it, expect, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { fundraisingApi } from "@/entities/fundraising";
import DonatePage from "./DonatePage.vue";

vi.mock("@/entities/fundraising", () => ({
  fundraisingApi: {
    getFundraising: vi.fn(() => new Promise(() => {})),
  },
}));

const render = () =>
  mount(DonatePage, {
    global: {
      stubs: {
        PageTitle: true,
        BlockTitle: { template: "<h2 class='block-title'><slot /></h2>" },
        RouterLink: {
          props: ["to"],
          template:
            "<a class='router-link' :data-to='JSON.stringify(to)'><slot /></a>",
        },
      },
    },
  });

/** The goal as the server answers it. */
const answering = (goal: { title: string; goalAmount: number }) =>
  vi.mocked(fundraisingApi.getFundraising).mockResolvedValueOnce({
    data: {
      resource: { ...goal, collectedAmount: 0, modifiedUtc: "2026-11-03" },
    },
    error: null,
  } as never);

const headings = (wrapper: ReturnType<typeof render>) =>
  wrapper.findAll(".block-title").map((title) => title.text());

// The bar is always there: a live site starts with nothing announced.
describe("the fundraising of the help page", () => {
  it("keeps the bar while nothing is announced", async () => {
    answering({ title: "", goalAmount: 0 });
    const wrapper = render();
    await flushPromises();

    expect(headings(wrapper)).toEqual(["Сбор средств", "Чем еще помочь"]);
    expect(wrapper.get(".progress").text()).toMatch(/^0\s\/\s0\sр\.$/);
    expect(wrapper.text()).toContain("объявленный сбор");
  });

  it("names a declared goal over its sum", async () => {
    answering({ title: "Сервер на год", goalAmount: 12000 });
    const wrapper = render();
    await flushPromises();

    expect(headings(wrapper)).toEqual(["Сбор средств", "Чем еще помочь"]);
    expect(wrapper.get(".goal-title").text()).toBe("Сервер на год");
    expect(wrapper.text()).toContain("0 / 12");
  });
});

describe("the page of help to the project", () => {
  it("sends a found fault to the forum's section of faults", () => {
    const wrapper = render();

    const link = wrapper
      .findAll(".router-link")
      .find((one) => one.text() === "сообщайте о находках");
    expect(link).toBeDefined();
    expect(JSON.parse(link!.attributes("data-to") ?? "null")).toEqual({
      name: "forum",
      params: { alias: "bugs" },
    });
  });
});
