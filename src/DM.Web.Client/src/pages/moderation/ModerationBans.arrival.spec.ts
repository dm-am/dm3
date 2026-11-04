/**
 * @vitest-environment jsdom
 */

// The entry the anchor names comes into view after a pause for its page to
// settle, and the page can be gone before the pause runs out. The rest of the
// page is ModerationBans.spec.ts's, which stands at the size limit.
import { it, expect, vi, afterEach } from "vitest";
import { defineComponent, h, nextTick, ref } from "vue";
import { flushPromises } from "@vue/test-utils";
import ModerationBans from "./ModerationBans.vue";
import { mountPage } from "@/shared/lib/testing/mountPage";

const { getBanHistory } = vi.hoisted(() => ({ getBanHistory: vi.fn() }));

// The network only: the router and the list are the real ones.
vi.mock("@/entities/moderation/api/banApi", async (importOriginal) => ({
  ...(await importOriginal<
    typeof import("@/entities/moderation/api/banApi")
  >()),
  default: { getBanHistory },
}));

afterEach(() => {
  vi.useRealTimers();
  delete (Element.prototype as Partial<Element>).scrollIntoView;
  document.body.innerHTML = "";
});

/** Entry 15 of the log, the one the address names. */
const entry = {
  number: 15,
  kind: "Issued",
  occurredUtc: "2026-10-20T09:30:00Z",
  ban: {
    id: "b-15",
    user: { id: "u-newcomer", username: "Newcomer" },
    moderator: { id: "u-moderator", username: "TestModerator" },
    type: "Permanent",
    accessPolicy: "FullBan",
    startedUtc: "2026-10-20T09:30:00Z",
    comment: "Реклама",
    isActive: true,
    canLift: false,
  },
};

it("leaves the page alone once it is gone before the entry came into view", async () => {
  // jsdom lays nothing out and has no scrollIntoView of its own.
  const scrolled: string[] = [];
  Element.prototype.scrollIntoView = function (this: Element) {
    scrolled.push(this.id);
  };
  getBanHistory.mockResolvedValue({
    data: { resources: [entry], paging: null },
    error: null,
  });
  // The list goes while the address still names its entry: an unmount of the
  // whole app would reset the address, and the address is no proof of the page.
  const shown = ref(true);
  const Shell = defineComponent({
    setup: () => () => (shown.value ? h(ModerationBans) : null),
  });
  vi.useFakeTimers({ toFake: ["setTimeout"] });
  await mountPage(Shell, "/moderation/bans#ban-15", {
    routes: [
      ["moderation-bans", "/moderation/bans"],
      ["profile", "/users/:username"],
    ],
    attachTo: document.body,
  });
  await flushPromises();

  shown.value = false;
  await nextTick();
  // Whatever stands under the same id once the page is gone is not the entry.
  const stranger = document.body.appendChild(document.createElement("div"));
  stranger.id = "ban-15";
  await vi.advanceTimersByTimeAsync(300);

  expect(scrolled).toEqual([]);
  expect(stranger.classList.contains("highlight-unread")).toBe(false);
});
