/**
 * @vitest-environment jsdom
 */

// The request is asserted with the markup: a page number nobody reads and a
// page size from a literal look identical on screen.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ref } from "vue";
import { flushPromises, enableAutoUnmount } from "@vue/test-utils";
import type { Router } from "vue-router";
import { mountPage } from "@/shared/lib/testing/mountPage";
import WarningsPage from "./WarningsPage.vue";

const getWarningLog = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  warningApi: { getWarningLog },
  WarningCard: {
    props: ["warning"],
    template: "<div class='card-stub'>{{ warning.reason }}</div>",
  },
  RecordSkeleton: {
    props: ["count", "withText"],
    template: "<div class='skeleton-stub' />",
  },
}));

// The reader's own page size, and a number no list on the site defaults to:
// a page that reads a literal instead answers with the literal.
const PAGE_SIZE = 7;
vi.mock("@/shared/lib/composables/usePaging", () => ({
  usePaging: () => ({ pageSize: ref(PAGE_SIZE) }),
}));

const entry = (over: Record<string, unknown> = {}) => ({
  id: "w-1",
  user: { username: "Нарушитель" },
  moderator: { username: "Модератор" },
  points: 2,
  reason: "Оскорбление",
  createdUtc: "2026-05-01T12:00:00",
  ...over,
});

const page = (resources: unknown[], total = resources.length) => ({
  data: {
    resources,
    paging: { skip: 0, take: PAGE_SIZE, total, current: 1, pages: 1 },
  },
  error: null,
});

enableAutoUnmount(afterEach);

let router: Router;

/** The log at `address`; the router of the last mount is `router`. */
async function rendered(address = "/warnings") {
  const page = await mountPage(WarningsPage, address, {
    routes: [["warnings", "/warnings"]],
    stubs: {
      Paging: {
        props: ["paging"],
        template: "<nav class='paging-stub' />",
      },
      ErrorState: {
        props: ["message", "retry"],
        template: "<div class='error-stub'>{{ message }}</div>",
      },
    },
  });
  router = page.router;
  await flushPromises();
  return page.wrapper;
}

const NARROWED = `/warnings?username=${encodeURIComponent("Нарушитель")}`;

describe("the open warning log", () => {
  beforeEach(() => {
    getWarningLog.mockReset();
  });

  it("draws a card per warning in force", async () => {
    getWarningLog.mockResolvedValue(
      page([entry(), entry({ id: "w-2", reason: "Флуд" })]),
    );

    const wrapper = await rendered();

    expect(wrapper.findAll(".card-stub").map((card) => card.text())).toEqual([
      "Оскорбление",
      "Флуд",
    ]);
    expect(wrapper.find(".paging-stub").exists()).toBe(true);
  });

  it("asks for the page named in the address, sized by the reader", async () => {
    getWarningLog.mockResolvedValue(page([entry()]));

    await rendered("/warnings?number=3");

    expect(getWarningLog).toHaveBeenCalledWith({
      username: undefined,
      skip: 2 * PAGE_SIZE,
      take: PAGE_SIZE,
    });
  });

  it("narrows to one person, and says whose warnings these are", async () => {
    getWarningLog.mockResolvedValue(page([entry()]));

    const wrapper = await rendered(NARROWED);

    expect(getWarningLog).toHaveBeenCalledWith({
      username: "Нарушитель",
      skip: 0,
      take: PAGE_SIZE,
    });
    expect(wrapper.find("h1").text()).toBe("Лог предупреждений: Нарушитель");
    expect(wrapper.find(".lead-text a").text()).toBe("Лог предупреждений");
  });

  it("is titled for the whole website when nothing narrows it", async () => {
    getWarningLog.mockResolvedValue(page([entry()]));

    const wrapper = await rendered();

    expect(wrapper.find("h1").text()).toBe("Лог предупреждений");
    expect(wrapper.find(".lead-text").exists()).toBe(false);
  });

  it("holds the shape of the list while it loads", async () => {
    let release: ((value: unknown) => void) | undefined;
    getWarningLog.mockReturnValue(
      new Promise((resolve) => {
        release = resolve;
      }),
    );

    const wrapper = await rendered();
    expect(wrapper.find(".skeleton-stub").exists()).toBe(true);

    release?.(page([entry()]));
    await flushPromises();
    expect(wrapper.find(".skeleton-stub").exists()).toBe(false);
  });

  it("offers another try when the log cannot be read", async () => {
    getWarningLog.mockResolvedValue({ data: null, error: { status: 500 } });

    const wrapper = await rendered();

    expect(wrapper.find(".error-stub").text()).toBe(
      "Не удалось загрузить предупреждения",
    );
  });

  it("says the log is empty, and says it differently under a filter", async () => {
    getWarningLog.mockResolvedValue(page([]));
    expect((await rendered()).text()).toContain("Предупреждений пока нет");

    getWarningLog.mockResolvedValue(page([]));
    expect((await rendered(NARROWED)).text()).toContain(
      "Предупреждений по заданным фильтрам не найдено",
    );
  });

  // The reader is told and offered the way back, filter kept.
  it("offers the first page when the number is past the end of a filled log", async () => {
    getWarningLog.mockResolvedValue(page([], 3));

    const wrapper = await rendered(`${NARROWED}&number=9`);

    expect(wrapper.text()).toContain("На этой странице предупреждений нет");
    const back = wrapper.find(".warnings-empty a");
    expect(back.text()).toBe("Вернуться на первую страницу");
    expect(back.attributes("href")).toBe(NARROWED);

    await back.trigger("click");
    await flushPromises();
    expect(router.currentRoute.value.fullPath).toBe(NARROWED);
  });

  // A second click of the paging control is enough to put two pages on the wire.
  it("keeps the page the address names when a later answer belongs to an older one", async () => {
    const answers = new Map<number, (value: unknown) => void>();
    getWarningLog.mockImplementation(
      ({ skip }: { skip: number }) =>
        new Promise((resolve) => {
          answers.set(skip, resolve);
        }),
    );

    const wrapper = await rendered();

    await router.push("/warnings?number=2");
    await flushPromises();

    answers.get(PAGE_SIZE)?.(page([entry({ id: "w-2", reason: "Флуд" })]));
    await flushPromises();
    answers.get(0)?.(page([entry()]));
    await flushPromises();

    expect(wrapper.findAll(".card-stub").map((card) => card.text())).toEqual([
      "Флуд",
    ]);
  });
});
