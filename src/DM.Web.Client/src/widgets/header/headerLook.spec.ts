/**
 * @vitest-environment node
 */

/**
 * Two things of the header a reader's finger and eye meet, read out of its
 * compiled stylesheet: nothing in jsdom lays them out.
 *
 * The counts of the greeting, "(0/4)", are a few pixels of figure each. On a
 * phone they used to be two squares of 44 with a badge; now they are what the
 * desktop shows, and a finger finds each by a field at least 24 wide and 32
 * high around it, out of the flow, so the line does not move. The first
 * reaches left, the second right, and the two meet in the middle of the slash
 * between them instead of covering each other.
 *
 * The paper strip is the header's own, under the whole of it, however high
 * a greeting that wraps makes it. It used to be drawn by the shell at a fixed
 * height, which knew nothing of a taller header. Its torn edge is drawn at
 * the height it has on a header of one row, 100, whatever the height of the
 * header: stretched to a taller header, the edge came out larger than on 1366.
 *
 * The button of the sliding column weighs what the logo beside it weighs: the
 * ink of the text, a sign 22 across. Up to the shell breakpoint the logo
 * stands in the middle of the screen, the button at the left edge, in the
 * middle of the block of the logo and the line under it (owner, 13 October
 * 2026: not of the logo alone).
 *
 * The greeting is the whole name: a line too long for its box wraps between
 * its words inside the box, and a name wider than the box breaks at its edge.
 */
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  componentCss,
  styleRules,
  type Declarations,
} from "@/shared/lib/testing/styles";

const HERE = dirname(fileURLToPath(import.meta.url));
const HEADER = resolve(HERE, "Header.vue");
const RULES = styleRules(componentCss(HEADER));

function declared(selector: string): Declarations {
  const found = RULES.filter((rule) => rule.selector === selector);
  expect(found, selector).toHaveLength(1);
  return found[0].declarations;
}

const pxOf = (value: string | undefined) => parseFloat(value ?? "");

describe("the counts of the greeting", () => {
  it("are each found by a field 24 wide and 32 high, out of the flow", () => {
    const link = declared(".count");
    const field = declared(".count::after");

    expect(link.position).toBe("relative");
    expect(field.position).toBe("absolute");
    expect(field.content).toBe('""');
    expect(pxOf(field["min-width"])).toBeGreaterThanOrEqual(24);
    expect(pxOf(field.height)).toBeGreaterThanOrEqual(32);
    // Centred on the line of the figure, whatever its height.
    expect(field.top).toBe("50%");
    expect(field.transform).toBe("translateY(-50%)");
  });

  it("meet in the slash: the first reaches left of it, the second right", () => {
    const first = declared(".count--messages::after");
    const second = declared(".count--notifications::after");
    const reach = pxOf(
      declared(".count::after").width.match(/\+ ([\d.]+)px/)?.[1],
    );

    // Each field runs past its figure into the slash by the same few pixels,
    // and from there away from the other.
    expect(first).toEqual({ right: `-${reach}px` });
    expect(second).toEqual({ left: `-${reach}px` });
    expect(reach).toBeGreaterThan(0);
    expect(reach).toBeLessThan(4);

    const source = readFileSync(HEADER, "utf8");
    expect(source).toMatch(
      /class="count count--messages"[\s\S]*data-testid="unread-messages"/,
    );
    expect(source).toMatch(
      /class="count count--notifications"[\s\S]*data-testid="notifications"/,
    );
  });
});

describe("the button of the sliding column", () => {
  it("is drawn in the ink of the text, as the logo beside it is", () => {
    const toggle = declared(".menu-toggle");
    expect(toggle.color).toBe("var(--text)");
    expect(declared(".menu-toggle:hover").color).toBe("var(--link-hover)");
  });

  it("is 44 square, its sign 22 across in the proportions of the sign", () => {
    const toggle = declared(".menu-toggle");
    expect(toggle.width).toBe("44px");
    expect(toggle.height).toBe("44px");
    const sign = declared(".menu-toggle .menu-sign");
    expect(sign.width).toBe("22px");
    // The viewBox of the sign is 16 by 12.
    expect(sign.height).toBe("16.5px");
  });

  it("stands in the middle of the logo and the line under it, down too", () => {
    // In the grid of the header, not over it at a height summed by hand: its
    // box spans the header less the baseline padding, the box the logo and
    // the line under it are centred in as one block, and it centres itself
    // there too. However many lines the greeting takes, the two middles meet.
    const button = declared(".menu-button");
    expect(button["grid-area"]).toBe("button");
    expect(button.position).toBe("relative");
    expect(button.top).toBeUndefined();
    expect(button.display).toBe("flex");
    expect(button["align-items"]).toBe("center");
    const info = RULES.filter((rule) => rule.selector === ".user-info");
    const infoPad = info.find((rule) => rule.declarations["padding-bottom"])!
      .declarations["padding-bottom"];
    expect(button.padding.split(/\s+/)[2]).toBe(infoPad);
    expect(
      info.some((rule) => rule.declarations["justify-content"] === "center"),
    ).toBe(true);
    // Nothing aligns the grid row to its start or end.
    expect(declared(".header--narrow")["align-items"]).toBeUndefined();
  });
});

describe("the header of a narrow screen", () => {
  it("is a grid of the button's side at either edge and the logo between", () => {
    const narrow = declared(".header--narrow");
    // A button of 44 and 8 on either side of it, and as much on the right.
    expect(narrow["grid-template-columns"]).toBe("60px minmax(0, 1fr) 60px");
    expect(narrow["grid-template-areas"]).toBe('"button user ."');
  });

  it("puts the drawing of the logo in the middle, at its size of 1366", () => {
    expect(declared(".header--narrow .user-info")["align-items"]).toBe(
      "center",
    );
    const logo = declared(".header--narrow .logo");
    // 271.29 is the drawing of 1366, 55 high.
    expect(logo.width).toMatch(/^271\.2\d*px$/);
    expect(logo["margin-left"]).toBe("0");
    // No smaller logo of its own below some width.
    expect(
      RULES.filter(
        (rule) => /\.logo$/.test(rule.selector) && rule.declarations.height,
      ).map((rule) => rule.declarations.height),
    ).toEqual(["55px"]);
  });

  it("has no column for the menu and the statistics, which are in the sliding column", () => {
    expect(declared(".header--narrow")["grid-template-areas"]).not.toMatch(
      /menu|stats/,
    );
  });
});

describe("the greeting", () => {
  it("wraps between its words inside its box, and only when it does not fit", () => {
    // The line under the logo keeps the width of its words while they fit
    // (each part of the line is flex: none), so a name that fits stands
    // where it stood. A longer one is held to the box and wraps there; a
    // name wider than the box breaks at its edge (word-wrap of the page).
    const greeting = declared(".greeting");
    expect(greeting["white-space"]).toBe("normal");
    expect(greeting["max-width"]).toBe("100%");
    expect(greeting["text-align"]).toBe("center");
    expect(declared(".user-actions > *").flex).toBe("none");
    // The guest's line and the logo keep their one line.
    const info = RULES.filter((rule) => rule.selector === ".user-info");
    expect(
      info.some((rule) => rule.declarations["white-space"] === "nowrap"),
    ).toBe(true);
  });

  it("has no spacer up to the shell breakpoint, where the logo itself is in the middle", () => {
    expect(declared(".header--narrow .user-actions::after").display).toBe(
      "none",
    );
  });
});

describe("the paper strip", () => {
  it("is the header's own, its torn edge as high as on a header of one row", () => {
    const strip = declared(".header::before");

    expect(strip.position).toBe("absolute");
    expect(strip.inset).toBe("0");
    expect(strip.filter).toBe("var(--filter-invert)");
    // Two layers of the one picture at the height of a header of one row.
    // The first is the torn edge at the bottom; the second stands at the top
    // in the same grain, and only its flat paper shows above the first.
    const pictures = strip["background-image"].split(/,\s*/);
    expect(pictures).toHaveLength(2);
    for (const picture of pictures)
      expect(picture).toContain("header-decoration.png");
    expect(strip["background-position"]).toBe("left bottom, left top");
    expect(strip["background-size"]).toBe("auto 100px");
    expect(strip["background-repeat"]).toBe("repeat-x");
  });

  it("grows with the header, which grows with what it holds", () => {
    // The header of one row is at least 100 and grows when its greeting
    // wraps, at every width.
    const header = RULES.filter(
      (rule) =>
        rule.selector === ".header" && "min-height" in rule.declarations,
    );
    expect(header).toHaveLength(1);
    expect(header[0].declarations["min-height"]).toBe("100px");
    expect(
      RULES.filter(
        (rule) => rule.selector === ".header" && "height" in rule.declarations,
      ),
    ).toEqual([]);
    expect(declared(".header--narrow").height).toBeUndefined();
  });

  it("is not drawn by the shell at a height of its own", () => {
    const app = readFileSync(resolve(HERE, "../../app/App.vue"), "utf8");

    expect(app).not.toContain("header-decoration");
  });
});
