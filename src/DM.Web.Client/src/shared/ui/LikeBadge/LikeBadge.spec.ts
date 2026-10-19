/**
 * @vitest-environment jsdom
 */

/**
 * The like in each of its states: "Нравится" alone while nobody likes it,
 * "Нравится (3)" once somebody does, bold for the viewer who does, and the
 * plain words for the author's own content or a guest, which are no control
 * and are not drawn while there is nobody to count. The tooltip lists the
 * likers and stays away while there are none.
 *
 * Its paint is read off the compiled stylesheet: the reader's mark is bold,
 * the words take the line's colour, and red is nowhere in it.
 */
import { describe, it, expect } from "vitest";
import { mount, config } from "@vue/test-utils";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import type { Username } from "@/shared/api/models/common";
import { Tooltip } from "@/shared/ui/Tooltip";
import Button from "@/shared/ui/Button/Button.vue";
import LikeBadge from "./LikeBadge.vue";

const HERE = dirname(fileURLToPath(import.meta.url));
const SHEET = componentCss(resolve(HERE, "LikeBadge.vue"));

// Rendered in place, not into <body>: the convention the tooltip specs use.
config.global.stubs = { Teleport: true };

const likers = (...names: string[]) =>
  names.map((username) => ({ username: username as Username }));

function render(props: {
  likes: { username: Username }[];
  liked?: boolean;
  interactive?: boolean;
}) {
  return mount(LikeBadge, { props });
}

describe("LikeBadge: what it says", () => {
  it("is the word alone while nobody likes it", () => {
    const wrapper = render({ likes: [], interactive: true });
    expect(wrapper.get(".like-badge").text()).toBe("Нравится");
  });

  it("carries the count in brackets once somebody does", () => {
    const wrapper = render({ likes: likers("a", "b", "c"), interactive: true });
    expect(wrapper.get(".like-badge").text()).toBe("Нравится (3)");
  });

  it("is bold for the viewer who likes it, and only then", () => {
    const on = render({ likes: likers("me"), liked: true, interactive: true });
    expect(on.get(".like-badge").classes()).toContain("like-badge--pressed");

    const off = render({ likes: likers("b"), interactive: true });
    expect(off.get(".like-badge").classes()).not.toContain(
      "like-badge--pressed",
    );
  });

  it("says the same for the author and a guest", () => {
    const wrapper = render({ likes: likers("a", "b") });
    expect(wrapper.get(".like-badge").text()).toBe("Нравится (2)");
  });

  it("is not drawn for the author or a guest while nobody likes it", () => {
    const wrapper = render({ likes: [] });
    expect(wrapper.find(".like-badge").exists()).toBe(false);
    expect(wrapper.find(".tooltip-trigger").exists()).toBe(false);
  });
});

describe("LikeBadge: as a control", () => {
  it("is a toggle button named by its words", () => {
    const off = render({ likes: likers("a"), interactive: true });
    const button = off.get("button.like-badge");
    expect(button.attributes("type")).toBe("button");
    expect(button.attributes("aria-pressed")).toBe("false");
    expect(button.attributes("aria-label")).toBeUndefined();

    const on = render({ likes: likers("me"), liked: true, interactive: true });
    expect(on.get("button.like-badge").attributes("aria-pressed")).toBe("true");
  });

  // The app registers Button globally: the like must not wear its height and corners.
  it("stays a native button beside the globally registered Button", () => {
    const wrapper = mount(LikeBadge, {
      props: { likes: likers("a"), interactive: true },
      global: { components: { Button } },
    });
    expect(wrapper.findComponent(Button).exists()).toBe(false);
    expect(wrapper.get("button.like-badge").attributes("type")).toBe("button");
  });

  it("asks the caller to toggle, by mouse and by keyboard alike", async () => {
    const wrapper = render({ likes: [], interactive: true });
    // Enter and Space on a native button arrive as a click.
    await wrapper.get("button").trigger("click");
    expect(wrapper.emitted("toggle")).toHaveLength(1);
  });

  it("is no control for the author or a guest", async () => {
    const wrapper = render({ likes: likers("a", "b") });
    expect(wrapper.find("button").exists()).toBe(false);
    await wrapper.get(".like-badge").trigger("click");
    expect(wrapper.emitted("toggle")).toBeUndefined();
  });
});

describe("LikeBadge: the likers", () => {
  it("lists them in the tooltip, in both variants", () => {
    for (const interactive of [false, true]) {
      const wrapper = render({ likes: likers("Anna", "Boris"), interactive });
      const tooltip = wrapper.getComponent(Tooltip);
      expect(tooltip.props("text")).toBe("Нравится: Anna, Boris");
      expect(tooltip.props("disabled")).toBe(false);
    }
  });

  it("names five and counts the rest", () => {
    const wrapper = render({
      likes: likers("A", "B", "C", "D", "E", "F", "G", "H", "I"),
      interactive: true,
    });
    expect(wrapper.getComponent(Tooltip).props("text")).toBe(
      "Нравится: A, B, C, D, E и еще 4",
    );

    const five = render({ likes: likers("A", "B", "C", "D", "E") });
    expect(five.getComponent(Tooltip).props("text")).toBe(
      "Нравится: A, B, C, D, E",
    );
  });

  it("has no tooltip while there is nobody to list", () => {
    const wrapper = render({ likes: [], interactive: true });
    expect(wrapper.getComponent(Tooltip).props("disabled")).toBe(true);
  });

  it("lets the keyboard reach the static words, the button alone otherwise", () => {
    const listed = render({ likes: likers("a") });
    expect(listed.get(".tooltip-trigger").attributes("tabindex")).toBe("0");

    // The button takes focus itself; a second stop on its wrapper would be a
    // tab stop with nothing to press.
    const button = render({ likes: likers("a"), interactive: true });
    expect(
      button.get(".tooltip-trigger").attributes("tabindex"),
    ).toBeUndefined();
  });
});

describe("LikeBadge: paint", () => {
  const rules = styleRules(SHEET);

  it("marks the reader's own like bold, over the button's font reset", () => {
    const pressed = rules.find(({ selector }) =>
      /\.like-badge\.like-badge--pressed/.test(selector),
    );
    expect(pressed?.declarations["font-weight"]).toBe("700");
  });

  it("takes the colour of its line", () => {
    const base = rules.find(({ selector }) => selector === ".like-badge");
    expect(base?.declarations.color).toBe("inherit");
  });

  it("is red nowhere", () => {
    expect(SHEET).not.toMatch(/accent-red/);
  });
});
