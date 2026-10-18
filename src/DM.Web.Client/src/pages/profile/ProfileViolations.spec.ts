/**
 * @vitest-environment jsdom
 */

/**
 * The violations line of a profile, and where its points lead.
 *
 * "N/6" was a dead number: it said how much a person had been warned and gave
 * no way to find out what for. It now opens the open log narrowed to that
 * person, where every warning carries its reason.
 *
 * What decides the link is the number of warnings in force, not the points on
 * them. A verbal warning costs nothing and still stands in the log, so a line
 * reading "0/6" can have entries behind it; a profile with no warnings at all
 * must not offer a link to an empty page.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import type { Username } from "@/shared/api/models/common";
import ProfileViolations from "./ProfileViolations.vue";

const getWarnings = vi.hoisted(() => vi.fn());
const getBans = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/moderation")>()),
  moderationApi: { getWarnings, getBans },
}));

const warnings = (totalPoints: number, activeCount: number) => ({
  data: { username: "Нарушитель", totalPoints, activeCount, warnings: [] },
  error: null,
});

const noBans = {
  data: { username: "Нарушитель", isBanned: false, history: [] },
  error: null,
};

async function render() {
  const wrapper = mount(ProfileViolations, {
    props: { username: "Нарушитель" as Username },
    global: {
      stubs: {
        RouterLink: {
          props: ["to"],
          template:
            "<a class='router-link' :data-to='JSON.stringify(to)'><slot /></a>",
        },
      },
    },
  });
  await flushPromises();
  return wrapper;
}

describe("the violations line", () => {
  beforeEach(() => {
    getWarnings.mockReset();
    getBans.mockReset();
    getBans.mockResolvedValue(noBans);
  });

  it("takes the points to the log of this user's warnings", async () => {
    getWarnings.mockResolvedValue(warnings(2, 1));

    const wrapper = await render();

    const link = wrapper.find(".stat-line a");
    expect(link.text()).toBe("2/6");
    expect(JSON.parse(link.attributes("data-to") ?? "null")).toEqual({
      name: "warnings",
      query: { username: "Нарушитель" },
    });
  });

  it("links a verbal warning too, though it costs no points", async () => {
    getWarnings.mockResolvedValue(warnings(0, 1));

    const wrapper = await render();

    expect(wrapper.find(".stat-line a").text()).toBe("0/6");
  });

  it("leaves the points as text when there is nothing to open", async () => {
    getWarnings.mockResolvedValue(warnings(0, 0));

    const wrapper = await render();

    expect(wrapper.find(".stat-line").text()).toBe("Нарушения: 0/6");
    expect(wrapper.find(".stat-line a").exists()).toBe(false);
  });
});
