/**
 * @vitest-environment jsdom
 */

/**
 * The events line of the global chat, on its DOM side: what it says before
 * the list answers, when the list is refused and when it is empty; and that
 * it says all of it, every word and the title whole, on any width.
 *
 * A document that lays nothing out cannot say where a line wraps, so the wrap
 * itself is the live stand's to show; chatParity.spec.ts holds that nothing
 * in the strip's dress keeps it on one line.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { nextTick } from "vue";
import type {
  GlobalChatEvent,
  GlobalChatEventSummary,
} from "@/entities/global-chat";

const getEvents = vi.fn();
vi.mock("@/entities/global-chat/api/globalChatApi", () => ({
  default: { getEvents: (...args: unknown[]) => getEvents(...args) },
}));

import { useGlobalChatStore } from "@/entities/global-chat";
import ChatEventsPanel from "./ChatEventsPanel.vue";

const HOUR = 3600 * 1000;

const summary = (
  id: string,
  status: "Live" | "Scheduled",
  startsUtc: string,
  isOpen = true,
): GlobalChatEventSummary =>
  ({
    id,
    title: `Эвент ${id}`,
    startsUtc,
    status,
    isOpen,
    participantCount: 1,
  }) as GlobalChatEventSummary;

/** A live event that ends in two hours, and two scheduled after it. */
function liveAndTwo(): GlobalChatEventSummary[] {
  const now = Date.now();
  return [
    summary("live", "Live", new Date(now - HOUR).toISOString()),
    summary("next", "Scheduled", new Date(now + 26 * HOUR).toISOString()),
    summary(
      "later",
      "Scheduled",
      new Date(now + 98 * HOUR).toISOString(),
      false,
    ),
  ];
}

let wrapper: VueWrapper | null = null;

/** The panel over the store as given, mounted and measured. */
async function render(
  load: "pending" | "loaded" | "failed",
  events: GlobalChatEventSummary[] = [],
) {
  const store = useGlobalChatStore();
  store.events = events;
  store.eventsLoad = load;
  store.eventDetails.live = {
    id: "live",
    duration: "03:00:00",
  } as unknown as GlobalChatEvent;
  wrapper = mount(ChatEventsPanel, {
    attachTo: document.body,
    global: {
      stubs: {
        ChatEventActions: true,
        RouterLink: true,
        SecondaryText: true,
        ContentText: true,
      },
    },
  });
  await settled();
  return wrapper;
}

/** Lets the renders and the measures the line asked for run. */
async function settled() {
  for (let round = 0; round < 4; round += 1) {
    await nextTick();
    await Promise.resolve();
  }
}

const line = (w: VueWrapper) => w.get(".row-main");

// The clock stands at noon: the live event ends two hours from now, and after
// 22:00 that end fell on the next day and was written with its date, so the
// line under test changed with the hour the suite ran at. Only Date is faked;
// the renders still run on real ticks.
const NOON = new Date(2026, 6, 26, 12, 0);

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(NOON);
  setActivePinia(createPinia());
  getEvents.mockReset();
});

afterEach(() => {
  vi.useRealTimers();
  wrapper?.unmount();
  wrapper = null;
  document.body.innerHTML = "";
});

describe("the events line says only what the list said", () => {
  it("says the events did not load when the read was refused", async () => {
    const w = await render("failed");

    expect(line(w).text()).toBe("Эвенты не загрузились | повторить");
    expect(w.text()).not.toContain("Нет запланированных эвентов");
  });

  it("says there are none only after an answer with none", async () => {
    const w = await render("loaded");

    expect(line(w).text()).toBe("Нет запланированных эвентов");
  });

  it("holds a bar and says nothing before the list has answered", async () => {
    const w = await render("pending");

    expect(w.find(".row-placeholder").exists()).toBe(true);
    expect(line(w).text()).toBe("");
  });

  it("keeps the events of an earlier read over a refused re-read", async () => {
    const w = await render("failed", liveAndTwo());

    expect(line(w).text()).toContain("Идет: Эвент live");
    expect(line(w).text()).not.toContain("не загрузились");
  });
});

describe("повторить", () => {
  it("reads the list again, with the bar while the read is out", async () => {
    let answer!: (value: unknown) => void;
    getEvents.mockReturnValue(
      new Promise((resolve) => {
        answer = resolve;
      }),
    );
    const w = await render("failed");

    await w.get(".row-failed button").trigger("click");
    await settled();
    expect(getEvents).toHaveBeenCalledTimes(1);
    expect(w.find(".row-placeholder").exists()).toBe(true);

    answer({ data: { resources: liveAndTwo() }, error: null });
    await settled();
    expect(line(w).text()).toContain("Идет: Эвент live");
  });

  it("hands the focus to what stands in its place once the read is back", async () => {
    getEvents.mockResolvedValueOnce({ data: null, error: { status: 503 } });
    const w = await render("failed");

    const first = w.get(".row-failed button").element as HTMLElement;
    first.focus();
    first.click();
    await settled();
    // Refused again: "повторить" is back, and so is the focus.
    expect(document.activeElement?.textContent?.trim()).toBe("повторить");

    getEvents.mockResolvedValueOnce({
      data: { resources: liveAndTwo() },
      error: null,
    });
    (document.activeElement as HTMLElement).click();
    await settled();
    // The events are back: the focus is on "описание", the first control of
    // the line, which opens what the line is about.
    expect(document.activeElement?.textContent?.trim()).toBe("описание");
  });
});

describe("the line says all of it on any width", () => {
  it("says the focal event, the description and the count whole", async () => {
    const w = await render("loaded", liveAndTwo());

    expect(line(w).text()).toBe(
      "Идет: Эвент live, до 14:00 | описание | +2 запланировано, ближайший\u00a027.07",
    );
    // Nothing has left the line and nothing in it is cut to a width.
    expect(line(w).findAll("[hidden]")).toEqual([]);
    expect(line(w).findAll("[style]")).toEqual([]);
    expect(line(w).findAll("[title]")).toEqual([]);
  });

  /**
   * "ближайший" and its day are one unit of the line. On a 360 phone the line
   * broke between them, and "27.07" stood alone on a line of its own under
   * the word it belongs to. The space between them does not break, as the one
   * after "в" in a date and a time does (CODE_STYLE), and a copy still reads
   * it as a plain space.
   */
  it('keeps "ближайший" and its day together', async () => {
    const w = await render("loaded", liveAndTwo());

    const near = line(w).get(".rest-near").text();
    expect(near).toBe(", ближайший\u00a027.07");
    expect(near).not.toMatch(/ближайший /);
  });

  it("names the event in text, the two links of the line beside it", async () => {
    const w = await render("loaded", liveAndTwo());

    expect(line(w).get(".row-title").element.tagName).toBe("SPAN");
    expect(
      line(w)
        .findAll("button")
        .map((b) => b.text()),
    ).toEqual(["описание", "+2 запланировано"]);
  });
});

describe("a line of the upcoming list", () => {
  it('says the title, the date and "закрытый" whole', async () => {
    const w = await render("loaded", liveAndTwo());

    const items = w.findAll(".rest-item");
    expect(items.map((item) => item.text())).toEqual([
      "Эвент next, 27.07 в\u00a014:00",
      "Эвент later, 30.07 в\u00a014:00, закрытый",
    ]);
    for (const item of items) expect(item.findAll("[style]")).toEqual([]);
  });
});
