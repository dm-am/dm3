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
 *
 * The number itself is a link and not a copy button: it leads where it points,
 * the way "Перейти к посту" does in the footer of the post.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { createMemoryHistory, createRouter } from "vue-router";
import { Tooltip } from "@/shared/ui/Tooltip";
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
} as unknown as PostReview;

/**
 * The room page with the post anchored and this review named, as GamePost
 * hands it down.
 */
const ROUTE = {
  name: "game-room",
  params: { id: "abcde", num: 2 },
  query: { review: "r-1" },
  hash: "#post-p-1",
};

const ROUTES = [
  { path: "/", name: "home", component: { template: "<div />" } },
  // The author of the review is a link to their profile.
  {
    path: "/users/:username",
    name: "profile",
    component: { template: "<div />" },
  },
  {
    path: "/game/:id/rooms/:num",
    name: "game-room",
    component: { template: "<div />" },
  },
];

const mountItem = () => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  return mount(PostReviewItem, {
    props: { review, number: 1, to: ROUTE },
    global: { plugins: [router] },
  });
};

describe("PostReviewItem", () => {
  it("copies as one human line, with the comma and the space before the number", () => {
    const line = mountItem().get(".review-meta").element.textContent;

    expect(line).toBe("+1 от Тест Елки, 26.07.2026 в 16:00, #1");
  });

  it("describes the anchor with the project tooltip instead of a native title", () => {
    const wrapper = mountItem();
    const anchor = wrapper.get(".review-anchor");

    expect(anchor.attributes("title")).toBeUndefined();
    expect(anchor.attributes("aria-label")).toBe("Перейти к оценке поста");
    expect(wrapper.findComponent(Tooltip).props("text")).toBe(
      "Перейти к оценке поста",
    );
  });

  it("leads to the address it was handed instead of copying it", () => {
    const wrapper = mountItem();

    // A link, so the address is in the markup: the browser's own "copy link"
    // still reaches it, and a middle click opens it in a tab — neither of
    // which a button that wrote to the clipboard could do.
    expect(wrapper.find("button.review-anchor").exists()).toBe(false);
    expect(wrapper.get(".review-anchor").attributes("href")).toBe(
      "/game/abcde/rooms/2?review=r-1#post-p-1",
    );
  });
});
