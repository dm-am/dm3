/**
 * @vitest-environment jsdom
 */

/** The two blocks of text are hyphenated while "Перенос слов" is on; the rolls never. */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import type { Post } from "@/entities/game";
import { useUiStore } from "@/shared/stores";
import { clearRegistry } from "@/shared/lib/composables/useExpandableRegistry";
import {
  post,
  render,
  stubResizeObserver,
  type PostWrapper,
} from "./GamePost.fixtures";

enableAutoUnmount(afterEach);

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

stubResizeObserver();

describe("GamePost typesetting", () => {
  const withEverything = {
    ...post,
    metagameText: "<p>Метаигровой текст</p>",
    diceRolls: [
      { rolls: 1, edges: 20, bonus: 0, results: [{ value: 7 }], comment: "" },
    ],
  } as unknown as Post;

  const classesOf = (wrapper: PostWrapper, selector: string) =>
    wrapper.get(selector).classes();

  beforeEach(() => {
    localStorage.clear();
    clearRegistry();
  });

  afterEach(() => {
    clearRegistry();
  });

  it("hyphenates both blocks of text by default, and not the rolls", async () => {
    const wrapper = await render("/", withEverything);

    for (const block of [".game-text", ".metagame-text"]) {
      expect(classesOf(wrapper, block)).toContain("hyphenated");
    }
    expect(classesOf(wrapper, ".dice-rolls")).not.toContain("hyphenated");
    wrapper.unmount();
  });

  it("drops hyphenation the reader turned off on a previous visit", async () => {
    localStorage.setItem("dm_hyphenate_words", "off");
    const wrapper = await render("/", withEverything);

    for (const block of [".game-text", ".metagame-text"]) {
      expect(classesOf(wrapper, block)).not.toContain("hyphenated");
    }
    wrapper.unmount();
  });

  it("follows the panel without a reload", async () => {
    const wrapper = await render("/", withEverything);
    const ui = useUiStore();

    ui.setHyphenateWords(false);
    await flushPromises();
    expect(classesOf(wrapper, ".game-text")).not.toContain("hyphenated");

    ui.setHyphenateWords(true);
    await flushPromises();
    expect(classesOf(wrapper, ".metagame-text")).toContain("hyphenated");
    wrapper.unmount();
  });
});
