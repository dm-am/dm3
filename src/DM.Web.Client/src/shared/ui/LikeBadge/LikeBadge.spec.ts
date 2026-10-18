/**
 * @vitest-environment jsdom
 */

/**
 * The like badge in each of the states the owner approved: no likes (the bare
 * hollow heart), some likes (a plate with the count), liked by the viewer (the
 * heart filled), and the same drawing for the author's own content or a guest,
 * which is not a control.
 *
 * Its paint is the other half, and the half that went wrong before: the hearts
 * it replaces turned $accent-red when liked and under the pointer. The owner's
 * rule is that heart and plate stay the colour of the line, so the stylesheet
 * is compiled and read: the colour is inherited, nothing reacts to hover or
 * press, and red is nowhere in it.
 *
 * The plate is a fill of the line's own colour with digits in the ink of a
 * solid fill, so those two are measured against each other in both palettes,
 * with the line's colour read from CaptionLine itself.
 */
import { describe, it, expect } from "vitest";
import { mount, config } from "@vue/test-utils";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap, styleRules } from "@/shared/lib/testing/styles";
import type { Username } from "@/shared/api/models/common";
import { Tooltip } from "@/shared/ui/Tooltip";
import SvgIcon from "@/shared/ui/Icon/SvgIcon.vue";
import LikeBadge from "./LikeBadge.vue";

const HERE = dirname(fileURLToPath(import.meta.url));
const SHEET = componentCss(resolve(HERE, "LikeBadge.vue"));
const RULES = ruleMap(SHEET);

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

describe("LikeBadge: what it draws", () => {
  it("is a bare hollow heart while nobody has liked it", () => {
    const wrapper = render({ likes: [], interactive: true });
    expect(wrapper.getComponent(SvgIcon).props("name")).toBe("heartEmpty");
    expect(wrapper.find(".like-badge__count").exists()).toBe(false);
  });

  it("carries the count in a plate once somebody has", () => {
    const wrapper = render({ likes: likers("a", "b", "c"), interactive: true });
    expect(wrapper.getComponent(SvgIcon).props("name")).toBe("heartEmpty");
    expect(wrapper.get(".like-badge__count").text()).toBe("3");
  });

  it("fills the heart for the viewer who liked it", () => {
    const wrapper = render({
      likes: likers("me", "b"),
      liked: true,
      interactive: true,
    });
    expect(wrapper.getComponent(SvgIcon).props("name")).toBe("heartFilled");
    expect(wrapper.get(".like-badge__count").text()).toBe("2");
  });

  it("draws the same for the author and a guest, filled or not", () => {
    for (const liked of [false, true]) {
      const wrapper = render({ likes: likers("a", "b", "c"), liked });
      expect(wrapper.getComponent(SvgIcon).props("name")).toBe(
        liked ? "heartFilled" : "heartEmpty",
      );
      expect(wrapper.get(".like-badge__count").text()).toBe("3");
    }
  });
});

describe("LikeBadge: as a control", () => {
  it("is a toggle button that says whether it is pressed", () => {
    const off = render({ likes: likers("a"), interactive: true });
    const button = off.get("button.like-badge");
    expect(button.attributes("type")).toBe("button");
    expect(button.attributes("aria-pressed")).toBe("false");
    expect(button.attributes("aria-label")).toBe("Нравится: 1");

    const on = render({ likes: likers("me"), liked: true, interactive: true });
    expect(on.get("button.like-badge").attributes("aria-pressed")).toBe("true");
  });

  it("names the action alone while there is no count to say", () => {
    const wrapper = render({ likes: [], interactive: true });
    expect(wrapper.get("button").attributes("aria-label")).toBe("Нравится");
  });

  it("asks the caller to toggle, by mouse and by keyboard alike", async () => {
    const wrapper = render({ likes: [], interactive: true });
    const button = wrapper.get("button");
    await button.trigger("click");
    // Enter and Space on a native button arrive as a click.
    expect(wrapper.emitted("toggle")).toHaveLength(1);
  });

  it("is no control for the author or a guest", async () => {
    const wrapper = render({ likes: likers("a", "b") });
    expect(wrapper.find("button").exists()).toBe(false);
    const drawing = wrapper.get(".like-badge");
    expect(drawing.attributes("role")).toBe("img");
    expect(drawing.attributes("aria-label")).toBe("Нравится: 2");
    await drawing.trigger("click");
    expect(wrapper.emitted("toggle")).toBeUndefined();
  });
});

describe("LikeBadge: the likers", () => {
  it("lists them in the tooltip, in both variants", () => {
    for (const interactive of [false, true]) {
      const wrapper = render({ likes: likers("Anna", "Boris"), interactive });
      const tooltip = wrapper.getComponent(Tooltip);
      expect(tooltip.props("text")).toBe("Anna и Boris оценили это");
      expect(tooltip.props("disabled")).toBe(false);
    }
  });

  it("stays quiet while there is nobody to list", () => {
    for (const interactive of [false, true]) {
      const wrapper = render({ likes: [], interactive });
      expect(wrapper.getComponent(Tooltip).props("disabled")).toBe(true);
    }
  });

  it("lets the keyboard reach the static drawing only when it has names", () => {
    const listed = render({ likes: likers("a") });
    expect(listed.get(".tooltip-trigger").attributes("tabindex")).toBe("0");

    const empty = render({ likes: [] });
    expect(
      empty.get(".tooltip-trigger").attributes("tabindex"),
    ).toBeUndefined();

    // The button takes focus itself; a second stop on its wrapper would be a
    // tab stop with nothing to press.
    const button = render({ likes: likers("a"), interactive: true });
    expect(
      button.get(".tooltip-trigger").attributes("tabindex"),
    ).toBeUndefined();
  });
});

describe("LikeBadge: paint", () => {
  it("takes the line's colour and font", () => {
    const base = RULES.get(".like-badge") ?? {};
    expect(base.color).toBe("inherit");
    expect(base.font).toBe("inherit");
    expect(base.background).toBe("none");
  });

  it("changes nothing under the pointer or the press", () => {
    const reacting = styleRules(SHEET)
      .map(({ selector }) => selector)
      .filter((selector) => /:hover|:active/.test(selector));
    expect(reacting).toEqual([]);
  });

  it("is red nowhere", () => {
    expect(SHEET).not.toMatch(/accent-red/);
  });

  it("fills the plate with the line's colour and cuts it like an award plate", () => {
    const plate = RULES.get(".like-badge__count") ?? {};
    expect(plate["background-color"]).toBe("currentColor");
    expect(plate["border-radius"]).toBe("4px");
    expect(plate["font-weight"]).toBe("700");
    expect(plate["font-variant-numeric"]).toBe("tabular-nums");
    expect(plate.border).toBe(
      "2px solid var(--like-badge-surface, var(--bg-element))",
    );
    expect(RULES.get(".like-badge__digits")?.color).toBe("var(--text-on-fill)");
  });
});

// --- the digits on the plate ------------------------------------------------

const CLIENT_SRC = resolve(HERE, "../../..");
const THEMES = readFileSync(
  resolve(CLIENT_SRC, "assets/styles/ThemeVariables.css"),
  "utf8",
).replace(/\/\*[\s\S]*?\*\//g, "");

function palette(header: string): Record<string, string> {
  const at = THEMES.indexOf(header);
  if (at < 0) throw new Error(`ThemeVariables.css has no "${header}" block`);
  const body = THEMES.slice(at + header.length);
  const tokens: Record<string, string> = {};
  for (const [, name, value] of body
    .slice(0, body.indexOf("}"))
    .matchAll(/^\s*--([\w-]+):\s*([^;]+);/gm)) {
    tokens[name] = value.trim();
  }
  return tokens;
}

/** `var(--x)` down to the hex the palette gives it. */
function hex(value: string, tokens: Record<string, string>): string {
  let current = value;
  for (let step = 0; step < 8; step += 1) {
    const alias = /^var\(--([\w-]+)\)$/.exec(current);
    if (!alias) break;
    current = tokens[alias[1]] ?? "";
  }
  if (/^#[0-9a-f]{3}$/i.test(current)) {
    current = current.replace(/[0-9a-f]/gi, (digit) => digit + digit);
  }
  if (!/^#[0-9a-f]{6}$/i.test(current)) {
    throw new Error(`${value} is not a solid colour here: ${current}`);
  }
  return current;
}

function luminance(colour: string): number {
  const [r, g, b] = [1, 3, 5]
    .map((at) => parseInt(colour.slice(at, at + 2), 16) / 255)
    .map((c) =>
      c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4),
    );
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(one: string, other: string): number {
  const [light, dark] = [luminance(one), luminance(other)].sort(
    (a, b) => b - a,
  );
  return (light + 0.05) / (dark + 0.05);
}

describe("LikeBadge: the digits read on the plate", () => {
  it("in both palettes, on the colour of the caption line", () => {
    const line = ruleMap(
      componentCss(resolve(HERE, "../CaptionLine/CaptionLine.vue")),
    ).get(".caption-line")?.color;
    const ink = RULES.get(".like-badge__digits")?.color;
    expect(line).toBeDefined();
    expect(ink).toBeDefined();

    for (const header of ["html.theme_Light {", "html.theme_Dark {"]) {
      const tokens = palette(header);
      const measured = contrast(
        hex(line as string, tokens),
        hex(ink as string, tokens),
      );
      // Body text, WCAG 1.4.3: the digits are 9px.
      expect(measured, header).toBeGreaterThanOrEqual(4.5);
    }
  });
});
