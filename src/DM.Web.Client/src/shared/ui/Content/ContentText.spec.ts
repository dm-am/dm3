/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import ContentText from "./ContentText.vue";
import { NSFW_SHOW_TEXT } from "@/shared/lib/utils/bbcodeConstants";

/**
 * A shocking block as the server writes it. Making it interactive rewrites the
 * words of its head, so a mark put there before that step would be lost: the
 * marks inside the head are what tells the order of the two steps apart.
 */
const SHOCKING =
  '<div class="nsfw-head">шокирующий</div><div class="nsfw-spoiler">Текст</div>';

const marksIn = (root: Element) =>
  [...root.querySelectorAll("mark.search-highlight")].map((m) => m.textContent);

async function render(html: string, searchQuery = "") {
  const wrapper = mount(ContentText, {
    props: { html, searchQuery },
    attachTo: document.body,
  });
  await nextTick();
  await nextTick();
  return wrapper;
}

describe("ContentText", () => {
  it("makes the text interactive once it is on the page", async () => {
    const wrapper = await render(SHOCKING);

    const head = wrapper.get(".nsfw-head");
    expect(head.attributes("role")).toBe("button");
    expect(head.text()).toBe(NSFW_SHOW_TEXT);
    wrapper.unmount();
  });

  it("marks the query after the text is made interactive, words it wrote included", async () => {
    const wrapper = await render(SHOCKING, "шокирующий");

    expect(marksIn(wrapper.get(".nsfw-head").element)).toEqual(["шокирующий"]);
    wrapper.unmount();
  });

  it("marks the query in a text that came later, and makes that text interactive", async () => {
    const wrapper = await render("<p>Первый абзац</p>", "абзац");

    await wrapper.setProps({ html: `<p>Второй абзац</p>${SHOCKING}` });
    await nextTick();

    expect(marksIn(wrapper.element)).toEqual(["абзац"]);
    expect(wrapper.get(".nsfw-head").attributes("role")).toBe("button");
    wrapper.unmount();
  });

  it("moves the marks to a new query and leaves none of the old one", async () => {
    const wrapper = await render("<p>Кот и пес</p>", "кот");

    await wrapper.setProps({ searchQuery: "пес" });
    await nextTick();

    expect(marksIn(wrapper.element)).toEqual(["пес"]);
    expect(wrapper.get("p").text()).toBe("Кот и пес");
    wrapper.unmount();
  });

  it("takes every mark away when the query is emptied", async () => {
    const wrapper = await render("<p>Кот и кот</p>", "кот");
    expect(marksIn(wrapper.element)).toEqual(["Кот", "кот"]);

    await wrapper.setProps({ searchQuery: "" });
    await nextTick();

    expect(marksIn(wrapper.element)).toEqual([]);
    expect(wrapper.get("p").element.childNodes).toHaveLength(1);
    wrapper.unmount();
  });
});
