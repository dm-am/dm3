/**
 * @vitest-environment jsdom
 */

/**
 * The rating line is one line of human text, and what the reader selects is
 * what has to reach the clipboard: "+1 от Тест Елки, 26.07.2026 в 16:00, #1".
 * It used to copy as "...в 16:00#1" — the gap before the number was drawn by
 * `margin-left` on the button, and a margin renders but does not copy.
 *
 * The assertion is on the raw textContent, deliberately not on a normalized
 * form. Whitespace that comes from template indentation instead of from the
 * copy is invisible in the browser and collapses on the way to the clipboard,
 * so a normalized comparison would pass over the very mistake this guards:
 * a separator that is geometry rather than a character.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { Tooltip } from "@/shared/ui";
import PostReviewItem from "./PostReviewItem.vue";
import type { PostReview } from "../model/types";

/**
 * No trailing "Z": formatDateFull renders local time, and a UTC stamp would
 * make the expected line depend on the timezone the suite runs in.
 */
const review = {
  id: "r-1",
  text: "<p>Отличный пост</p>",
  sign: "Positive",
  createdUtc: "2026-07-26T16:00:00",
  author: { username: "Тест Елки", role: "Player" },
  likes: [],
} as unknown as PostReview;

/**
 * The address of the room page with the post anchored and this review named,
 * as GamePost hands it down.
 */
const PERMALINK = "http://dm.am/game/abcde/rooms/2?review=r-1#post-p-1";

const mountItem = () =>
  mount(PostReviewItem, {
    props: { review, number: 1, permalink: PERMALINK },
    global: {
      components: { RouterLink: { template: "<a><slot /></a>" } },
    },
  });

describe("PostReviewItem", () => {
  it("copies as one human line, with the comma and the space before the number", () => {
    const line = mountItem().get(".review-meta").element.textContent;

    expect(line).toBe("+1 от Тест Елки, 26.07.2026 в 16:00, #1");
  });

  it("describes the anchor with the project tooltip instead of a native title", () => {
    const wrapper = mountItem();
    const anchor = wrapper.get(".review-anchor");

    expect(anchor.attributes("title")).toBeUndefined();
    expect(anchor.attributes("aria-label")).toBe("Скопировать ссылку на отзыв");
    expect(wrapper.findComponent(Tooltip).props("text")).toBe(
      "Скопировать ссылку на отзыв",
    );
  });

  it("copies the address it was handed, not the page it is drawn on", async () => {
    const written: string[] = [];
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: {
        writeText: (text: string) => {
          written.push(text);
          return Promise.resolve();
        },
      },
    });

    await mountItem().get(".review-anchor").trigger("click");

    // The old link was window.location.pathname plus a "#review-" hash: on
    // every surface that is not the room (the pulse, the home page, a profile)
    // it addressed that surface, and the hash was read by nobody.
    expect(written).toEqual([PERMALINK]);
    expect(written[0]).not.toContain("#review-");
  });
});
