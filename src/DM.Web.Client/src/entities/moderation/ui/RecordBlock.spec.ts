/**
 * @vitest-environment jsdom
 */

/**
 * The text of a record is set as the text of a comment is (CommentItem.vue,
 * .comment-text), as DM2 set every text block: 16px on a step of 20, flush on
 * both sides at every width, a phone included, for a phone shows the page of
 * the desktop. Its words break while "Перенос слов" of the settings panel is
 * on, as the words of a post and of a comment do; the page is in Russian
 * (index.html, lang="ru"), which is what the browser breaks the words by.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { useUiStore } from "@/shared/stores/ui";
import RecordBlock from "./RecordBlock.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

describe("the text of a record", () => {
  const css = componentCss(resolve(HERE, "RecordBlock.vue"));
  const merged = (selector: string) =>
    Object.assign(
      {},
      ...styleRules(css)
        .filter((rule) => rule.selector === selector)
        .map((rule) => rule.declarations),
    ) as Record<string, string>;

  it("is justified at every width, on the step of 20", () => {
    expect(merged(".record-block__text")).toMatchObject({
      "line-height": "1.25",
      "text-align": "justify",
    });
    expect(css).not.toMatch(/@media/);
  });

  it("breaks its words through the class of the switch alone", () => {
    expect(merged(".record-block__text.hyphenated")).toMatchObject({
      hyphens: "auto",
      "-webkit-hyphens": "auto",
    });
    // The switch turned off has to take the property with it.
    expect(merged(".record-block__text")).not.toHaveProperty("hyphens");
  });

  it("follows the switch of the settings panel", async () => {
    localStorage.clear();
    const wrapper = mount(RecordBlock, {
      global: { plugins: [createPinia()] },
      slots: { default: "Текст записи" },
    });
    const text = () => wrapper.get(".record-block__text").classes();
    expect(text()).toContain("hyphenated");

    useUiStore().setHyphenateWords(false);
    await flushPromises();

    expect(text()).not.toContain("hyphenated");
  });

  it("breaks its words where no store is installed, as the switch does by default", () => {
    setActivePinia(undefined);
    const wrapper = mount(RecordBlock, { slots: { default: "Текст записи" } });

    expect(wrapper.get(".record-block__text").classes()).toContain(
      "hyphenated",
    );
  });

  it("is in a page the browser knows to be Russian", () => {
    const page = readFileSync(resolve(HERE, "../../../../index.html"), "utf8");

    expect(page).toMatch(/<html lang="ru">/);
  });
});
