/**
 * @vitest-environment jsdom
 */

/**
 * The skeleton of a comment has the shape of the comment it stands for
 * (CommentItem), so nothing jumps when the comments come. The full layout is
 * the card of a post: the author's column of 140 beside the strips of the
 * text, at every width, the square of the avatar as wide as the content of the
 * column, the strips where the text starts, and the number under both. The
 * compact layout keeps its block without a column.
 *
 * Read off the two stylesheets: the numbers live in two components that
 * change apart.
 */
import { describe, it, expect, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { useUiStore } from "@/shared/stores/ui";
import CommentSkeleton from "./CommentSkeleton.vue";

const HERE = dirname(fileURLToPath(import.meta.url));
const SKELETON = resolve(HERE, "CommentSkeleton.vue");
const COMMENT = resolve(HERE, "../../../features/comment/ui/CommentItem.vue");

/** Every declaration of the rules that name the selector, alone or grouped. */
function declarationsOf(css: string, selector: string): Record<string, string> {
  return Object.assign(
    {},
    ...styleRules(css)
      .filter((rule) => rule.selector.split(", ").includes(selector))
      .map((rule) => rule.declarations),
  ) as Record<string, string>;
}

/** "11px 5px" and its kin as top, right, bottom, left numbers. */
function sides(shorthand: string): [number, number, number, number] {
  const n = shorthand.split(/\s+/).map((part) => parseFloat(part));
  const [top, right = top, bottom = top, left = right] = n;
  return [top, right, bottom, left];
}

describe("the skeleton of a comment", () => {
  const skeleton = componentCss(SKELETON);
  const comment = componentCss(COMMENT);

  it("pads its card as the comment does, in either layout", () => {
    expect(declarationsOf(skeleton, ".skeleton-comment").padding).toBe(
      declarationsOf(comment, ".comment").padding,
    );
    expect(
      declarationsOf(skeleton, ".skeleton-comment:not(.compact)").padding,
    ).toBe(
      declarationsOf(comment, ".comment:not(.compact):not(.removed)").padding,
    );
  });

  it("keeps its column beside the strips at every width, as the comment does", () => {
    expect(skeleton).not.toMatch(/@media/);
    expect(declarationsOf(skeleton, ".skeleton-columns")).not.toHaveProperty(
      "flex-direction",
    );
    expect(declarationsOf(skeleton, ".skeleton-columns").gap).toBe(
      declarationsOf(comment, ".comment:not(.compact) .comment-columns").gap,
    );
  });

  it("draws the author's column, its content as wide as the comment's", () => {
    const column = declarationsOf(skeleton, ".skeleton-meta");
    expect(column.width).toBe(declarationsOf(comment, ".comment-header").width);
    // The padding stands in for the margin inside the column of the comment,
    // so the width has to hold it, as the 140 of the comment does.
    expect(column["box-sizing"]).toBe("border-box");

    // The skeleton pads its column by what the comment spends around its
    // content: the margin of .author-inner and its padding on the right.
    const inner = declarationsOf(comment, ".author-inner");
    const [top, right, bottom, left] = sides(inner.margin);
    const padRight = parseFloat(inner["padding-right"] ?? "0");
    expect(sides(column.padding)).toEqual([
      top,
      right + padRight,
      bottom,
      left,
    ]);

    const content = parseFloat(column.width) - left - right - padRight;
    expect(content).toBe(128);
    const avatar = declarationsOf(skeleton, ".skeleton-avatar");
    expect(avatar).toMatchObject({
      width: `${content}px`,
      height: `${content}px`,
    });
    expect(avatar).not.toHaveProperty("border-radius");
  });

  it("starts the strips where the text of the comment starts", () => {
    const [bodyTop] = sides(
      declarationsOf(comment, ".comment:not(.compact) .comment-body").padding,
    );
    const [textTop, textRight, , textLeft] = sides(
      declarationsOf(comment, ".comment:not(.compact) .comment-text").margin,
    );
    const [top, right, , left] = sides(
      declarationsOf(skeleton, ".skeleton-text").padding,
    );

    expect(top).toBe(bodyTop + textTop);
    expect([right, left]).toEqual([textRight, textLeft]);
  });

  describe("on the page", () => {
    beforeEach(() => {
      localStorage.clear();
      setActivePinia(createPinia());
    });

    it("puts the column beside the text and the number under both in the full layout", () => {
      useUiStore().setMessageLayout("full");
      const card = mount(CommentSkeleton, { props: { count: 1 } }).get(
        ".skeleton-comment",
      );

      expect(
        [...card.element.children].map((child) => child.className),
      ).toEqual(["skeleton-columns", "skeleton-number"]);
      expect(
        [...card.get(".skeleton-columns").element.children].map(
          (child) => child.className,
        ),
      ).toEqual(["skeleton-meta", "skeleton-text"]);
      expect(card.find(".skeleton-meta .skeleton-avatar").exists()).toBe(true);
    });

    it("keeps the compact layout without a column", () => {
      useUiStore().setMessageLayout("compact");
      const card = mount(CommentSkeleton, { props: { count: 1 } }).get(
        ".skeleton-comment",
      );

      expect(card.classes()).toContain("compact");
      expect(card.find(".skeleton-columns").exists()).toBe(false);
      expect(card.find(".skeleton-avatar").exists()).toBe(false);
      expect(card.find(".skeleton-body .skeleton-content").exists()).toBe(true);
    });
  });
});
