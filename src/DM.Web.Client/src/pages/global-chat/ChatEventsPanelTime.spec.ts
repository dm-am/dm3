/**
 * @vitest-environment jsdom
 */

/**
 * The times of the events strip are on the site's clock: the account's zone
 * for a signed-in reader, the device's for a guest. The messages under the
 * strip were already stamped that way, and the strip over them was not: a
 * reader whose account is in Tokyo read the feed in Tokyo and the events in
 * the zone of whatever device they sat at.
 *
 * The device is in Moscow for the file, the account in Tokyo, six hours apart,
 * and the moments are picked so that the two clocks disagree about the day as
 * well as the hour.
 */
const MACHINE_ZONE = process.env.TZ;
process.env.TZ = "Europe/Moscow";

import {
  afterAll,
  afterEach,
  beforeEach,
  describe,
  expect,
  it,
  vi,
} from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type {
  GlobalChatEvent,
  GlobalChatEventSummary,
} from "@/entities/global-chat";
import { setAccountTimeZone } from "@/shared/lib/utils/timeZone";

vi.mock("@/entities/global-chat/api/globalChatApi", () => ({
  default: { getEvents: vi.fn(), getEvent: vi.fn() },
}));

import { useGlobalChatStore } from "@/entities/global-chat";
import ChatEventsPanel from "./ChatEventsPanel.vue";

const summary = (
  id: string,
  startsUtc: string,
  status: "Live" | "Scheduled",
): GlobalChatEventSummary =>
  ({
    id,
    title: `Эвент ${id}`,
    startsUtc,
    status,
    isOpen: true,
    participantCount: 1,
  }) as GlobalChatEventSummary;

/** The strip with a live event of two hours and one scheduled after it. */
function render(liveStartsUtc: string) {
  const store = useGlobalChatStore();
  store.events = [
    summary("live", liveStartsUtc, "Live"),
    summary("next", "2026-09-28T18:30:00Z", "Scheduled"),
    summary("later", "2026-09-30T18:30:00Z", "Scheduled"),
  ];
  store.eventDetails.live = {
    id: "live",
    duration: "02:00:00",
  } as unknown as GlobalChatEvent;
  return mount(ChatEventsPanel, {
    global: {
      stubs: {
        ChatEventActions: true,
        RouterLink: true,
        SecondaryText: true,
        ContentText: true,
      },
    },
  });
}

describe("the events strip for a reader in Tokyo on a device in Moscow", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    setAccountTimeZone("Asia/Tokyo");
  });

  afterEach(() => {
    vi.useRealTimers();
    setAccountTimeZone(null);
  });

  afterAll(() => {
    if (MACHINE_ZONE === undefined) delete process.env.TZ;
    else process.env.TZ = MACHINE_ZONE;
  });

  it("prints the end of the live event and the nearest day by the account", () => {
    // 09:00 in Tokyo, 03:00 in Moscow; the event ends at 10:09 in Tokyo.
    vi.useFakeTimers({ now: new Date("2026-09-27T00:00:00Z") });

    const text = render("2026-09-26T23:09:00Z").get(".row-main").text();

    expect(text).toContain("до 10:09");
    // 18:30 UTC on the 28th is the 29th in Tokyo and still the 28th in Moscow.
    expect(text).toContain("ближайший\u00a029.09");
  });

  it("prints the scheduled ones by the account", () => {
    vi.useFakeTimers({ now: new Date("2026-09-27T00:00:00Z") });

    const rest = render("2026-09-26T23:09:00Z")
      .findAll(".rest-item")
      .map((item) => item.text());

    expect(rest).toEqual([
      "Эвент next, 29.09 в\u00a003:30",
      "Эвент later, 01.10 в\u00a003:30",
    ]);
  });

  it("dates an end past the account's midnight, not the device's", () => {
    // 23:30 in Tokyo and 17:30 in Moscow: the end at 01:00 is tomorrow in
    // Tokyo and still today, 19:00, in Moscow.
    vi.useFakeTimers({ now: new Date("2026-09-27T14:30:00Z") });

    const text = render("2026-09-27T14:00:00Z").get(".row-main").text();

    expect(text).toContain("до 28.09.2026 в\u00a001:00");
  });
});
