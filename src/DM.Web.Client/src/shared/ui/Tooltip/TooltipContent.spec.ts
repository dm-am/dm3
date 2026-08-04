/**
 * @vitest-environment jsdom
 */

/**
 * A picture in a [tipimg:] hint can fail to load, and what used to happen then
 * was an assignment to outerHTML. That takes the <img> out from under Vue's
 * patcher: Vue goes on believing the node is there, and the next update of the
 * subtree — reopening the same hint, or a change to the text it is built from —
 * works on an element that is no longer in the document. The replacement also
 * carried a hex colour of its own, one of the two in the whole client, so the
 * message ignored the theme; and it said "GIF" for every kind of image.
 *
 * The branch next to it printed "[Debug: type=...]" — unreachable today, but
 * standing in the production template, one refactor away from a reader.
 *
 * So: a flag, a rendered element, a token, and no debug branch.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { nextTick } from "vue";
import { mount } from "@vue/test-utils";
import TooltipContent from "./TooltipContent.vue";

const HERE = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(HERE, "TooltipContent.vue"), "utf8");

const openPopup = async (text: string) => {
  const wrapper = mount(TooltipContent, { props: { text } });
  await wrapper.find(".rich-text-trigger").trigger("mouseenter");
  await nextTick();
  return wrapper;
};

describe("TooltipContent", () => {
  it("shows the picture of a [tipimg:] hint", async () => {
    const wrapper = await openPopup(
      "[tipimg:https://example.test/a.png]наведи[/tipimg]",
    );
    expect(wrapper.find("img.popup-image").exists()).toBe(true);
  });

  it("replaces a picture that failed with markup Vue owns", async () => {
    const wrapper = await openPopup(
      "[tipimg:https://example.test/a.png]наведи[/tipimg]",
    );
    await wrapper.find("img.popup-image").trigger("error");
    await nextTick();

    expect(wrapper.find("img.popup-image").exists()).toBe(false);
    expect(wrapper.find(".popup-error").text()).toBe(
      "Не удалось загрузить изображение",
    );
  });

  it("still shows the caption of a [tip:] hint", async () => {
    const wrapper = await openPopup("[tip:пояснение]слово[/tip]");
    expect(wrapper.find(".rich-text-popup").text()).toBe("пояснение");
  });

  it("never writes into the DOM behind Vue", () => {
    expect(source).not.toContain("outerHTML");
  });

  it("carries no debug text into the production template", () => {
    expect(source).not.toContain("[Debug:");
  });

  it("takes its colours from the theme", () => {
    // The failure message was #ff6b6b, which belongs to no palette and follows
    // no theme.
    expect(source).not.toMatch(/#[0-9a-fA-F]{3,6}\b/);
  });
});
