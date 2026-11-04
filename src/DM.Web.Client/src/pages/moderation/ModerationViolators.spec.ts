/**
 * @vitest-environment jsdom
 */

// Each segment asks for its own list and is marked when pressed; a narrowed
// empty list is told from a site without violators; the last press wins.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  mount,
  flushPromises,
  enableAutoUnmount,
  type VueWrapper,
} from "@vue/test-utils";
import ModerationViolators from "./ModerationViolators.vue";

const getViolators = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  moderationApi: { getViolators },
  banTermLabel: () => "Бан",
}));

const violator = (name: string) => ({
  user: { id: `id-${name}`, username: name },
  points: 2,
  pointsThreshold: 6,
  lastWarningUtc: "2026-10-01T10:00:00Z",
  activeBan: null,
});

const list = (names: string[]) => ({
  data: { resources: names.map(violator) },
  error: null,
});

enableAutoUnmount(afterEach);

async function rendered() {
  const wrapper = mount(ModerationViolators, {
    global: {
      stubs: {
        "page-title": { template: "<h1><slot /></h1>" },
        // Registered on the app, so a mount of the page alone knows neither.
        FormField: {
          props: ["label"],
          template:
            "<div class='field-stub'><label>{{ label }}</label><slot /></div>",
        },
        UserLink: {
          props: ["user"],
          template: "<span class='user-stub'>{{ user.username }}</span>",
        },
        ErrorState: { template: "<div class='error-stub' />" },
      },
    },
  });
  await flushPromises();
  return wrapper;
}

const segments = (wrapper: VueWrapper) =>
  wrapper.findAll('[role="group"] button');

const segment = (wrapper: VueWrapper, words: string) => {
  const found = segments(wrapper).find((b) => b.text() === words);
  if (!found) throw new Error(`no segment ${words}`);
  return found;
};

const names = (wrapper: VueWrapper) =>
  wrapper.findAll(".user-stub").map((n) => n.text());

describe("the moderation list of violators", () => {
  beforeEach(() => {
    getViolators.mockReset();
    getViolators.mockResolvedValue(list(["Первый"]));
  });

  it("filters by segments", async () => {
    const wrapper = await rendered();

    expect(segments(wrapper).map((b) => b.text())).toEqual([
      "Все",
      "С баном",
      "Только баллы",
    ]);
    expect(segment(wrapper, "Все").attributes("aria-pressed")).toBe("true");
    expect(getViolators).toHaveBeenCalledWith("all");
  });

  it.each([
    ["С баном", "banned"],
    ["Только баллы", "points-only"],
  ])("asks for the list of %s", async (words, filter) => {
    const wrapper = await rendered();

    await segment(wrapper, words).trigger("click");
    await flushPromises();

    expect(getViolators).toHaveBeenLastCalledWith(filter);
    expect(segment(wrapper, words).attributes("aria-pressed")).toBe("true");
    expect(segment(wrapper, "Все").attributes("aria-pressed")).toBe("false");
  });

  it("asks nothing again for the segment already pressed", async () => {
    const wrapper = await rendered();

    await segment(wrapper, "Все").trigger("click");
    await flushPromises();

    expect(getViolators).toHaveBeenCalledTimes(1);
  });

  it("tells a list narrowed to nobody from a site with no violators", async () => {
    getViolators.mockResolvedValue(list([]));
    const wrapper = await rendered();

    expect(wrapper.text()).toContain("Нарушителей пока нет");

    await segment(wrapper, "С баном").trigger("click");
    await flushPromises();

    expect(wrapper.text()).toContain(
      "Нарушителей по заданным фильтрам не найдено",
    );
    expect(wrapper.text()).not.toContain("Нарушителей пока нет");
  });

  it("keeps the list of the segment pressed last when an older answer lands late", async () => {
    const wrapper = await rendered();
    const answers = new Map<string, (value: unknown) => void>();
    getViolators.mockImplementation(
      (filter: string) =>
        new Promise((resolve) => {
          answers.set(filter, resolve);
        }),
    );

    await segment(wrapper, "С баном").trigger("click");
    await segment(wrapper, "Только баллы").trigger("click");
    answers.get("points-only")?.(list(["Баллы"]));
    await flushPromises();
    answers.get("banned")?.(list(["Забаненный"]));
    await flushPromises();

    expect(names(wrapper)).toEqual(["Баллы"]);
  });
});
