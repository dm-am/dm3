/**
 * @vitest-environment jsdom
 */

// The placeholder stands where a testimonial will, so it must be as tall as one: the text below would jump otherwise.
import { describe, it, expect } from "vitest";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { mount } from "@vue/test-utils";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import TestimonialSkeleton from "./TestimonialSkeleton.vue";

const HERE = dirname(fileURLToPath(import.meta.url));
const BUBBLE = resolve(
  HERE,
  "../../../shared/ui/SpeechBubble/SpeechBubble.vue",
);

describe("TestimonialSkeleton", () => {
  it("is the bubble itself, with three lines of text and a footer line", () => {
    const wrapper = mount(TestimonialSkeleton, { props: { count: 2 } });

    const bubbles = wrapper.findAll("article.bubble-card");
    expect(bubbles).toHaveLength(2);
    for (const card of bubbles) {
      expect(card.findAll(".bubble-content > .skeleton-text-row")).toHaveLength(
        3,
      );
      expect(card.find(".bubble-footer .skeleton-author").exists()).toBe(true);
      expect(card.find(".bubble-footer .skeleton-date").exists()).toBe(true);
    }
  });

  it("draws a line of shimmer one line of the bubble's text tall", () => {
    const skeleton = ruleMap(
      componentCss(resolve(HERE, "TestimonialSkeleton.vue")),
    );
    const content = ruleMap(componentCss(BUBBLE)).get(
      ".bubble .bubble-content",
    );

    // The bubble's min-height counts its lines in this one unit.
    const lineHeight = content?.["line-height"];
    expect(lineHeight).toBe("var(--line-height, 1.5)");
    expect(skeleton.get(".skeleton-text-row")?.height).toBe(
      `calc(${lineHeight} * 1em)`,
    );
  });
});
