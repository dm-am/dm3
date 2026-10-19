/**
 * @vitest-environment node
 */

/**
 * Private text, [code] and a picture look as they look on DM2, and the same
 * wherever the page draws markup: the text of [private] is green text and
 * nothing else, and [code] a monospace face in the colour and size of the text
 * around it. The stylesheet every post, preview and help is drawn with
 * (_BbcodeContent.sass) is compiled here and read for what it draws.
 */
import { describe, it, expect } from "vitest";
import { join } from "node:path";
import {
  compileSass,
  componentCss,
  styleRules,
  type Declarations,
} from "@/shared/lib/testing/styles";
import { PALETTES } from "@/shared/lib/testing/themeColours";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";

const css = compileSass(
  [
    '@use "@/assets/styles/BbcodeContent" as *',
    ".bbcode-content",
    "  +bbcode-content",
  ].join("\n"),
);

/** Everything the markup styles declare for one element of the markup. */
const declared = (selector: string): Declarations => {
  const full = `.bbcode-content ${selector}`;
  const merged: Declarations = {};
  for (const rule of styleRules(css)) {
    const list = rule.selector.split(",").map((one) => one.trim());
    if (list.includes(full)) Object.assign(merged, rule.declarations);
  }
  return merged;
};

describe("hidden text", () => {
  it.each([".private-message", ".bb-private", ".private-text"])(
    "%s is green text and nothing else",
    (selector) => {
      expect(declared(selector)).toEqual({ color: "var(--text-green)" });
    },
  );

  it("is the green of DM2 in both themes", () => {
    expect(PALETTES.light["text-green"]).toBe("#696");
    expect(PALETTES.dark["text-green"]).toBe("#7dd17d");
  });
});

describe("who reads a hidden block", () => {
  // The tooltip over the green words names the readers, so no line does.
  it("draws no line of recipients after the block", () => {
    expect(css).not.toContain("private-message-header");
  });

  it("draws the leads as it draws any name: nothing of their own", () => {
    expect(css).not.toContain("private-message-lead");
  });

  // A sibling selector skips text: "[/private] Остальные молчат.\nДалее" has
  // the break after the sentence as the header's next element, and hiding it
  // glued "Остальные молчат.Далее" into one line. No rule of the markup hides
  // a line break by its neighbour.
  it("hides no line break the author typed", () => {
    for (const rule of styleRules(css)) {
      if (!/(^|[\s>+~])br\b/.test(rule.selector)) continue;
      expect(rule.declarations.display, rule.selector).not.toBe("none");
    }
  });
});

describe("the [code] button of the editor", () => {
  // On DM2 every tag of the toolbar is set in one face, "code" included. In
  // Courier New at 12px the caption read as grey beside its neighbours, as if
  // the button were shut.
  it("is set in the face of the toolbar, as on DM2", () => {
    const sheet = componentCss(
      join(CLIENT_SRC, "shared/ui/BBCodeEditor/BBCodeEditorToolbar.vue"),
    );
    const fonts = styleRules(sheet)
      .filter((rule) => rule.selector.includes(".tag-btn"))
      .map((rule) => rule.declarations["font-family"])
      .filter(Boolean);
    expect(fonts).toEqual(["inherit"]);
  });
});

describe("[code]", () => {
  it("is the monospace face in the line, in the colour and size of the text", () => {
    const code = declared(".code");
    expect(code).toEqual(declared("code"));
    expect(code["font-family"]).toBe('"Courier New", Courier, monospace');
    expect(code.color).toBe("inherit");
    expect(code.display).toBe("inline");
    expect(code.background).toBe("none");
    expect(code.border).toBe("0");
    expect(code["font-size"]).toBeUndefined();
  });
});

describe("a picture of the text", () => {
  // As on DM2: no taller than 500 pixels, whatever the height of the window.
  // A taller one, a map or a sheet, opens whole on a click.
  it("is no taller than 500 pixels, as on DM2", () => {
    expect(declared("img")["max-height"]).toBe(
      "var(--bb-image-max-height, 500px)",
    );
    expect(declared(".bb-image")["max-height"]).toBe(
      "var(--bb-image-max-height, 500px)",
    );
  });
});

/** The box the picture is drawn in, read off the stylesheet. */
describe("the height of a picture in the text", () => {
  it("keeps the picture whole in a box clamped in height alone", () => {
    expect(declared("img")["object-fit"]).toBe("contain");
    expect(declared(".bb-image")["object-fit"]).toBe("contain");
  });
});
