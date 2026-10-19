/**
 * @vitest-environment jsdom
 */

/**
 * The skeleton of the tag picker stands where the picker will (TagSelector),
 * so nothing jumps when the tags come: the gaps between groups, under a
 * heading and between rows are the picker's, a bubble is as tall as a tag, and
 * the heading's blank is set in the heading's font. A row never wraps, so the
 * rows asked for are the rows drawn at any width.
 *
 * Read off the stylesheets of both: the numbers live in two components that
 * change apart.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  compileSass,
  componentCss,
  ruleMap,
} from "@/shared/lib/testing/styles";
import TagSelectorSkeleton from "./TagSelectorSkeleton.vue";

const HERE = dirname(fileURLToPath(import.meta.url));
const skeleton = ruleMap(
  componentCss(resolve(HERE, "TagSelectorSkeleton.vue")),
);
const picker = ruleMap(
  componentCss(resolve(HERE, "../../../entities/game/ui/TagSelector.vue")),
);

/** The pixels of a length the stylesheet wrote with a variable. */
const px = (value: string | undefined) =>
  ruleMap(compileSass(`.probe\n  width: ${value}`)).get(".probe")?.width;

describe("the skeleton of the tag picker", () => {
  it("parts the groups as the picker does", () => {
    expect(skeleton.get(".tags-skeleton")?.gap).toBe(
      picker.get(".tags-groups")?.gap,
    );
  });

  it("leaves the heading's margin under its blank and the tags' gap between rows", () => {
    const tagsGap = picker.get(".category-tags")?.gap;
    expect(
      px(picker.get(".tag-category .category-title")?.["margin-bottom"]),
    ).toBe(px(tagsGap));
    expect(skeleton.get(".skeleton-group")?.gap).toBe(tagsGap);
    expect(skeleton.get(".skeleton-row")?.gap).toBe(tagsGap);
  });

  it("sets the heading's blank in the heading's font and a bubble at a tag's height", () => {
    expect(skeleton.get(".skeleton-title")?.["font-size"]).toBe(
      picker.get(".tag-category .category-title")?.["font-size"],
    );
    expect(skeleton.get(".skeleton-bubble")?.height).toBe(
      picker.get(".tag-button")?.["min-height"],
    );
  });

  it("draws the rows asked for, group by group, never wrapping one", () => {
    const wrapper = mount(TagSelectorSkeleton, { props: { groups: [3, 1] } });

    expect(
      wrapper
        .findAll(".skeleton-group")
        .map((g) => g.findAll(".skeleton-row").length),
    ).toEqual([3, 1]);
    expect(skeleton.get(".skeleton-row")?.["flex-wrap"]).toBeUndefined();
    expect(skeleton.get(".skeleton-row")?.overflow).toBe("hidden");
    expect(wrapper.attributes("aria-hidden")).toBe("true");
  });
});
