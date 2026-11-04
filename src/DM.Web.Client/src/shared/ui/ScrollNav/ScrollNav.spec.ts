/**
 * @vitest-environment jsdom
 */

/**
 * The floating column of buttons at the bottom right: "Развернуть все",
 * "Наверх", "Вниз" and the gear of the settings of the site.
 *
 * It stands at every width, and nothing takes it away: not the end of a
 * scroll, not the caret in a field. The gear is in it at every width, and its
 * bubble holds the settings.
 */
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { enableAutoUnmount, mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { setScrollContainer } from "@/shared/lib/scroll";
import ScrollNav from "./ScrollNav.vue";

enableAutoUnmount(afterEach);

/** The scroll container of the shell, as tall as `content` over `height`. */
function page(content: number, height = 800) {
  const main = document.createElement("div");
  main.className = "main";
  Object.defineProperty(main, "scrollHeight", { value: content });
  Object.defineProperty(main, "clientHeight", { value: height });
  document.body.appendChild(main);
  setScrollContainer(main);
  return main;
}

/**
 * The tooltips of the buttons come up on focus and measure their text on a
 * canvas jsdom does not have; what they say is not what this file is about.
 */
const PLAIN_TOOLTIP = { template: "<span><slot /></span>" };

async function mountNav() {
  const wrapper = mount(ScrollNav, {
    attachTo: document.body,
    global: { stubs: { Tooltip: PLAIN_TOOLTIP } },
  });
  await flushPromises();
  return wrapper;
}

let main: HTMLElement;

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
});

afterEach(() => {
  main?.remove();
  setScrollContainer(null);
});

describe("ScrollNav", () => {
  beforeEach(() => {
    main = page(3000);
  });

  it("scrolls the page to its top and to its bottom", async () => {
    const scrolled: ScrollToOptions[] = [];
    main.scrollTo = ((options: ScrollToOptions) =>
      scrolled.push(options)) as typeof main.scrollTo;
    const wrapper = await mountNav();

    await wrapper.get('[aria-label="Прокрутить вниз"]').trigger("click");
    await wrapper.get('[aria-label="Прокрутить наверх"]').trigger("click");

    expect(scrolled).toEqual([
      { top: 3000, behavior: "smooth" },
      { top: 0, behavior: "smooth" },
    ]);
  });

  it("opens the settings of the site from the gear", async () => {
    const wrapper = await mountNav();

    await wrapper.get('button[aria-label="Настройки сайта"]').trigger("click");

    const labels = wrapper
      .findAll(".bubble-content .settings-label")
      .map((label) => label.text());
    expect(labels).toEqual(["Верстка", "Тема", "Перенос слов"]);
  });

  it("closes the bubble on Escape and gives the caret back to the gear", async () => {
    const wrapper = await mountNav();
    const gear = wrapper.get('button[aria-label="Настройки сайта"]');
    await gear.trigger("click");

    document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }));
    await flushPromises();

    expect(wrapper.find(".bubble-content").exists()).toBe(false);
    expect(document.activeElement).toBe(gear.element);
  });

  it("closes the bubble on a press outside it", async () => {
    const wrapper = await mountNav();
    await wrapper.get('button[aria-label="Настройки сайта"]').trigger("click");

    document.body.click();
    await flushPromises();

    expect(wrapper.find(".bubble-content").exists()).toBe(false);
  });
});
