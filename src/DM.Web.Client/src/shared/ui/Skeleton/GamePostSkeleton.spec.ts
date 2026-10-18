/**
 * @vitest-environment jsdom
 */

/**
 * The skeleton of a post has the shape of the post it stands for (GamePost),
 * so nothing jumps when the post comes: the left column of 140 beside the
 * strips of the text, at every width, for a phone shows the post of 1366, the
 * square of the portrait as wide as the content of the column, and the strips
 * where the text starts, the cards as far apart as the posts of the list.
 *
 * Read off the two stylesheets: the numbers live in two components that
 * change apart.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import GamePostSkeleton from "./GamePostSkeleton.vue";

const HERE = dirname(fileURLToPath(import.meta.url));
const SKELETON = resolve(HERE, "GamePostSkeleton.vue");
const SRC = resolve(HERE, "../../..");
const POST = resolve(SRC, "widgets/game-post/GamePost.vue");
const ROOM = resolve(SRC, "pages/game/GameRoom.vue");
const FEEDS = [
  resolve(SRC, "widgets/pulse-feed/PulseFeed.vue"),
  resolve(SRC, "widgets/rated-posts/ui/RatedPostsList.vue"),
];

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

describe("the skeleton of a post", () => {
  const skeleton = componentCss(SKELETON);
  const post = componentCss(POST);

  it("keeps its column beside the strips at every width, as the post does", () => {
    expect(skeleton).not.toMatch(/@media/);
    expect(declarationsOf(skeleton, ".skeleton-columns")).not.toHaveProperty(
      "flex-direction",
    );
    expect(declarationsOf(skeleton, ".skeleton-columns").gap).toBe(
      declarationsOf(post, ".post-columns").gap,
    );
  });

  it("draws the column of the post, its content as wide as the post's", () => {
    const column = declarationsOf(skeleton, ".skeleton-meta");
    expect(column.width).toBe(declarationsOf(post, ".post-meta").width);
    // The padding stands in for the margin inside the column of the post, so
    // the width has to hold it, as the 140 of the post does.
    expect(column["box-sizing"]).toBe("border-box");

    // The skeleton pads its column by what the post spends around its
    // content: the margin of .meta-inner and its padding on the right.
    const inner = declarationsOf(post, ".meta-inner");
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
    expect(declarationsOf(skeleton, ".skeleton-avatar")).toMatchObject({
      width: `${content}px`,
      height: `${content}px`,
    });
  });

  it("starts the strips where the text of the post starts", () => {
    const [bodyTop] = sides(declarationsOf(post, ".post-body").padding);
    const [textTop, textRight, , textLeft] = sides(
      declarationsOf(post, ".game-text").margin,
    );
    const [top, right, , left] = sides(
      declarationsOf(skeleton, ".skeleton-body").padding,
    );

    expect(top).toBe(bodyTop + textTop);
    expect([right, left]).toEqual([textRight, textLeft]);
  });

  it("stands its cards as far apart as the posts of the list it stands for", () => {
    // The feeds, whose posts carry the way to their room.
    for (const feed of FEEDS)
      expect(declarationsOf(skeleton, ".game-post-skeleton").gap, feed).toBe(
        declarationsOf(componentCss(feed), ".posts-list").gap,
      );
    // A room, whose posts stand without it, 20 apart as on DM2.
    expect(declarationsOf(skeleton, ".game-post-skeleton.in-room").gap).toBe(
      "20px",
    );
    expect(
      mount(GamePostSkeleton, {
        props: { count: 3, showNavigation: false },
      }).classes(),
    ).toContain("in-room");
    expect(
      mount(GamePostSkeleton, { props: { count: 5 } }).classes(),
    ).not.toContain("in-room");
  });

  it("is as far apart in a room as the posts of the room", () => {
    expect(declarationsOf(componentCss(ROOM), ".posts-list").gap).toBe(
      declarationsOf(skeleton, ".game-post-skeleton.in-room").gap,
    );
  });
});
