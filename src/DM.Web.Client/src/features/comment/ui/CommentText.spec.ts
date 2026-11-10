/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  type VueWrapper,
} from "@vue/test-utils";
import { useUiStore } from "@/shared/stores/ui";
import { freshComment, mountComponent } from "./CommentItem.fixtures";

enableAutoUnmount(afterEach);

beforeEach(freshComment);

describe("CommentItem typesetting", () => {
  const textClasses = (wrapper: VueWrapper) =>
    wrapper.get(".comment-text").classes();

  it("hyphenates the text by default", async () => {
    const wrapper = await mountComponent(vi.fn());

    expect(textClasses(wrapper)).toContain("hyphenated");
  });

  it("drops hyphenation the reader turned off on a previous visit", async () => {
    localStorage.setItem("dm_hyphenate_words", "off");
    const wrapper = await mountComponent(vi.fn());

    expect(textClasses(wrapper)).not.toContain("hyphenated");
  });

  it("follows the panel without a reload", async () => {
    const wrapper = await mountComponent(vi.fn());
    const ui = useUiStore();

    ui.setHyphenateWords(false);
    await flushPromises();

    expect(textClasses(wrapper)).not.toContain("hyphenated");
  });
});
