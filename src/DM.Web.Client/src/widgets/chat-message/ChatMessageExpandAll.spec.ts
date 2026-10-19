/**
 * @vitest-environment jsdom
 */

/**
 * "Развернуть все" of the floating column and the spoilers of a chat message,
 * across what happens to a message after it is drawn: an edit replaces its
 * markup inside the same element, a removal folds the body away, and a row
 * scrolled out of the feed unmounts. The button answers the spoilers on the
 * screen, and only those.
 */
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import {
  flushPromises,
  mount,
  RouterLinkStub,
  type VueWrapper,
} from "@vue/test-utils";
import { clearRegistry } from "@/shared/lib/composables/useExpandableRegistry";
import ScrollNav from "@/shared/ui/ScrollNav/ScrollNav.vue";
import ChatMessage from "./ChatMessage.vue";

const mounted: VueWrapper[] = [];

const withSpoiler = (inside: string) =>
  `<p>Текст</p><a class="spoiler-head">Спойлер</a><div class="spoiler">${inside}</div>`;

const MESSAGE = {
  id: "m1",
  createdUtc: "2026-10-05T07:38:00Z",
  author: { username: "anna" },
  isContinuation: false,
  isRemoved: false,
  edits: [],
  likes: [],
};

async function render(text: string) {
  const message = mount(ChatMessage, {
    attachTo: document.body,
    props: { message: { ...MESSAGE, text } as never, maxHeight: 5000 },
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  // The tooltips measure their text on a canvas jsdom does not have.
  const nav = mount(ScrollNav, {
    attachTo: document.body,
    global: { stubs: { Tooltip: { template: "<span><slot /></span>" } } },
  });
  mounted.push(message, nav);
  await flushPromises();
  return { message, nav };
}

const toggleAll = (nav: VueWrapper) => nav.find(".toggle-all-btn");

const change = async (message: VueWrapper, patch: object) => {
  await message.setProps({
    message: { ...(message.props() as { message: object }).message, ...patch },
  });
  await flushPromises();
};

/** The spoiler of the body as the reader sees it: open or closed. */
const spoilerOpen = (message: VueWrapper) =>
  message.get(".spoiler-head").attributes("aria-expanded") === "true";

beforeEach(() => clearRegistry());

afterEach(() => {
  while (mounted.length) mounted.pop()?.unmount();
  document.body.innerHTML = "";
});

describe("Развернуть все over a chat message", () => {
  it("opens the spoiler of an edited message and then offers Свернуть все", async () => {
    const { message, nav } = await render(withSpoiler("до правки"));
    await change(message, { text: withSpoiler("после правки") });

    await toggleAll(nav).trigger("click");
    await flushPromises();

    expect(spoilerOpen(message)).toBe(true);
    expect(toggleAll(nav).attributes("aria-label")).toBe("Свернуть все");

    await toggleAll(nav).trigger("click");
    await flushPromises();

    expect(spoilerOpen(message)).toBe(false);
    expect(toggleAll(nav).attributes("aria-label")).toBe("Развернуть все");
  });

  it("goes away with the spoiler of a removed message", async () => {
    const { message, nav } = await render(withSpoiler("скрытое"));
    expect(toggleAll(nav).exists()).toBe(true);

    await change(message, { isRemoved: true });

    expect(toggleAll(nav).exists()).toBe(false);
  });

  it("goes away with a row that left the feed", async () => {
    const { message, nav } = await render(withSpoiler("скрытое"));

    message.unmount();
    mounted.splice(mounted.indexOf(message), 1);
    await flushPromises();

    expect(toggleAll(nav).exists()).toBe(false);
  });
});
