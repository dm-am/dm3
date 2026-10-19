/**
 * @vitest-environment jsdom
 */

/**
 * "Развернуть все" of the floating column and the spoilers of an element drawn
 * by v-bbcode-interactive: a new markup in the same element is bound again,
 * the element leaving the page lets its spoilers go. Every body of the site
 * (text, comment, topic, warning, chat message) binds through it.
 */
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { defineComponent, ref } from "vue";
import { clearRegistry } from "@/shared/lib/composables/useExpandableRegistry";
import ScrollNav from "@/shared/ui/ScrollNav/ScrollNav.vue";
import { vBbcodeInteractive } from "./bbcodeInteractive";

const withSpoiler = (inside: string) =>
  `<p>Текст</p><a class="spoiler-head">Спойлер</a><div class="spoiler">${inside}</div>`;

const html = ref("");
const shown = ref(true);

const Body = defineComponent({
  directives: { bbcodeInteractive: vBbcodeInteractive },
  setup: () => ({ html, shown }),
  template: `<div><div v-if="shown" v-bbcode-interactive="html" class="body" v-html="html" /></div>`,
});

const mounted: VueWrapper[] = [];

async function render(markup: string) {
  html.value = markup;
  shown.value = true;
  const body = mount(Body, { attachTo: document.body });
  // The tooltips measure their text on a canvas jsdom does not have.
  const nav = mount(ScrollNav, {
    attachTo: document.body,
    global: { stubs: { Tooltip: { template: "<span><slot /></span>" } } },
  });
  mounted.push(body, nav);
  await flushPromises();
  return { body, nav };
}

const toggleAll = (nav: VueWrapper) => nav.find(".toggle-all-btn");

const spoilerOpen = (body: VueWrapper) =>
  body.get(".spoiler-head").attributes("aria-expanded") === "true";

beforeEach(() => clearRegistry());

afterEach(() => {
  while (mounted.length) mounted.pop()?.unmount();
  document.body.innerHTML = "";
});

describe("v-bbcode-interactive and Развернуть все", () => {
  it("binds the spoiler of a new markup and lets the old one go", async () => {
    const { body, nav } = await render(withSpoiler("до правки"));
    html.value = withSpoiler("после правки");
    await flushPromises();

    await toggleAll(nav).trigger("click");
    await flushPromises();

    expect(spoilerOpen(body)).toBe(true);
    expect(toggleAll(nav).attributes("aria-label")).toBe("Свернуть все");

    await toggleAll(nav).trigger("click");
    await flushPromises();

    expect(spoilerOpen(body)).toBe(false);
    expect(toggleAll(nav).attributes("aria-label")).toBe("Развернуть все");
  });

  it("takes the button away with a markup that has no spoiler left", async () => {
    const { nav } = await render(withSpoiler("скрытое"));
    expect(toggleAll(nav).exists()).toBe(true);

    html.value = "<p>Без спойлера</p>";
    await flushPromises();

    expect(toggleAll(nav).exists()).toBe(false);
  });

  it("takes the button away with the element that left the page", async () => {
    const { nav } = await render(withSpoiler("скрытое"));

    shown.value = false;
    await flushPromises();

    expect(toggleAll(nav).exists()).toBe(false);
  });
});
