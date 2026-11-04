/**
 * @vitest-environment jsdom
 */

// The card of the focal event answers "Развернуть все" of the floating column,
// which stands outside the line, and a click anywhere else still closes it.
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import type {
  GlobalChatEvent,
  GlobalChatEventSummary,
} from "@/entities/global-chat";

vi.mock("@/entities/global-chat/api/globalChatApi", () => ({
  default: { getEvents: vi.fn(), getEvent: vi.fn() },
}));

import { useGlobalChatEventsStore } from "@/entities/global-chat";
import { clearRegistry } from "@/shared/lib/composables/useExpandableRegistry";
import ScrollNav from "@/shared/ui/ScrollNav/ScrollNav.vue";
import ChatEventsPanel from "./ChatEventsPanel.vue";

const mounted: VueWrapper[] = [];

/** The line over a live event, and the floating column beside it. */
async function render() {
  const store = useGlobalChatEventsStore();
  store.events = [
    {
      id: "live",
      title: "Эвент live",
      startsUtc: new Date(Date.now() - 3600 * 1000).toISOString(),
      status: "Live",
      isOpen: true,
      participantCount: 1,
    } as GlobalChatEventSummary,
  ];
  store.eventsLoad = "loaded";
  store.eventDetails.live = {
    id: "live",
    duration: "03:00:00",
  } as unknown as GlobalChatEvent;

  const panel = mount(ChatEventsPanel, {
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
  // The tooltips measure their text on a canvas jsdom does not have.
  const nav = mount(ScrollNav, {
    attachTo: document.body,
    global: { stubs: { Tooltip: { template: "<span><slot /></span>" } } },
  });
  mounted.push(panel, nav);
  await flushPromises();
  return { panel, nav };
}

const toggleAll = (nav: VueWrapper) => nav.get(".toggle-all-btn");

/** "описание", the item of the line that opens the card. */
const describeItem = (panel: VueWrapper) =>
  panel.get("[aria-controls='chat-event-overlay']");

const cardOpen = (panel: VueWrapper) =>
  describeItem(panel).attributes("aria-expanded") === "true";

beforeEach(() => {
  setActivePinia(createPinia());
  clearRegistry();
});

afterEach(() => {
  while (mounted.length) mounted.pop()?.unmount();
  document.body.innerHTML = "";
});

describe("the card of the focal event", () => {
  it("opens on Развернуть все, and the button then offers Свернуть все", async () => {
    const { panel, nav } = await render();
    expect(toggleAll(nav).attributes("aria-label")).toBe("Развернуть все");

    await toggleAll(nav).trigger("click");

    expect(cardOpen(panel)).toBe(true);
    expect(toggleAll(nav).attributes("aria-label")).toBe("Свернуть все");

    await toggleAll(nav).trigger("click");

    expect(cardOpen(panel)).toBe(false);
    expect(toggleAll(nav).attributes("aria-label")).toBe("Развернуть все");
  });

  it("closes on a click anywhere else on the page", async () => {
    const { panel } = await render();
    await describeItem(panel).trigger("click");
    expect(cardOpen(panel)).toBe(true);

    document.body.click();
    await flushPromises();

    expect(cardOpen(panel)).toBe(false);
  });
});
