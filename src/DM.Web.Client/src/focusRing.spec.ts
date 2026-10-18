/**
 * @vitest-environment node
 */

/**
 * A control that takes keyboard focus shows where the focus is, and every
 * control shows it the same way.
 *
 * The ring is one colour, $link, and it sits on the edge of the control. A
 * field turns its frame $link and adds a 1px shadow of the same colour flush
 * against it: 2px following the field's own shape, no outline. A block control
 * (button, tab, pill, picker trigger, [tabindex] box) draws `outline: 2px solid
 * $link` at offset 0 on :focus-visible; controls that stand flush against each
 * other pull the ring inward, a primary with the $link fill pushes it out by
 * 1px, and inline text takes a 1px ring instead. The geometry is the reader's
 * business (UI_STANDARDS.md, "Клавиатура и фокус"); the colour is measurable,
 * so it is measured here.
 *
 * Two things break it.
 *
 * A component hiding the ring on `:focus`. That reads as "no ring when
 * clicked", which is what the author meant, but `:focus-visible` is a subset of
 * `:focus`, so the rule takes the keyboard ring away as well — and it did, on
 * the search field, the date field, the range picker, the poll editor, the
 * password reset form, the fundraising page, the award series page, the chat
 * composer and the notepad. Between them that is most of the typing a person
 * does on the site, and a keyboard reader had nothing to go by. The remaining
 * border-colour change measured 1.8:1 against the resting state, under the 3:1
 * WCAG 2.4.11 asks for. The narrowing is `:focus:not(:focus-visible)`.
 *
 * A second colour. The site used to draw the ring in grey ($border-focus) from
 * the global sheet and in $link from the button mixin, and the two met on one
 * form: grey around the fields, blue around the button, at three different
 * offsets. Measured, the grey is 2.85 against the light page, under the 3:1
 * WCAG 2.4.13 asks of a focus indicator; $link clears it in both themes on
 * every surface a control stands on (fillContrast.spec.ts). So the grey token
 * has no place under any focus selector, and whatever draws a ring — an
 * outline, an outline-color, a spread-only box-shadow, the frame of a field —
 * draws it in $link.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import { compileSass, ruleMap } from "@/shared/lib/testing/styles";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);
const STYLE_FILES = [".vue", ".sass", ".scss", ".css"];

/** The one colour of the ring, in the two spellings the sheets use. */
const RING_COLOUR = /^(\$link|var\(--link\))$/;

/** The grey the ring used to be drawn in. Both spellings, so a raw custom property cannot smuggle it back. */
const GREY = /\$border-focus\b|--border-focus\b/;

/**
 * A selector that hides the ring on plain `:focus` is right anyway when the
 * block draws the ring itself. A field's frame plus a spread-only shadow of
 * $link is 2px of ring following the field's own shape; the outline is dropped
 * only because that pair replaces it. That is a rule, not a list: every
 * component that draws its own field frame follows it, and the check below
 * reads the block instead of naming the files.
 *
 * The editor frame is the one place the rule does not reach: `:focus-within`
 * reacts to focus landing inside the frame, and the control that took the
 * focus is the editable area within it.
 */
const ALLOWED_HIDE: Record<string, string> = {
  "shared/ui/BBCodeEditor/BBCodeEditor.vue":
    ":focus-within on the frame, not on a control",
};

/**
 * Where a ring is drawn in a colour other than $link, and why. The speech
 * bubble stands on the green fill, where $link has nothing to sit against; its
 * ring is the ink that already clears 3:1 there.
 */
const ALLOWED_COLOUR: Record<string, { colour: string; why: string }> = {
  "shared/ui/SpeechBubble/SpeechBubble.vue": {
    colour: "$text-on-green",
    why: "the ring stands on the green fill, where $link does not contrast",
  },
};

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (STYLE_FILES.some((ext) => name.endsWith(ext))) {
      out.push(full);
    }
  }
  return out;
}

const where = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

const indentOf = (line: string): number => line.match(/^\s*/)![0].length;
const isComment = (line: string): boolean => /^\s*(\/\/|\/\*|\*)/.test(line);
const isBlank = (line: string): boolean => !line.trim();

/**
 * The selectors a declaration sits under, innermost first. Each step is the
 * nearest line indented less than the current one, together with the
 * comma-terminated lines that lead into it, so a group like
 * `a:focus-visible,\nbutton:focus-visible` is read whole and not by its last
 * line. Comments are skipped: they sit at any indent and select nothing.
 */
function selectorsAbove(lines: string[], index: number): string[] {
  const chain: string[] = [];
  let indent = indentOf(lines[index]);
  for (let i = index - 1; i >= 0; i--) {
    if (isBlank(lines[i]) || isComment(lines[i])) continue;
    const own = indentOf(lines[i]);
    if (own >= indent) continue;
    const group = [lines[i].trim()];
    for (let j = i - 1; j >= 0; j--) {
      if (isBlank(lines[j]) || isComment(lines[j])) continue;
      if (indentOf(lines[j]) !== own || !lines[j].trim().endsWith(",")) break;
      group.unshift(lines[j].trim());
    }
    chain.push(group.join(" "));
    indent = own;
    if (own === 0) break;
  }
  return chain;
}

/** The declarations that share a block with the line at `index`: the run of lines at its indent or deeper. */
function blockAround(lines: string[], index: number): string[] {
  const indent = indentOf(lines[index]);
  const inBlock = (i: number): boolean =>
    isBlank(lines[i]) || isComment(lines[i]) || indentOf(lines[i]) >= indent;
  let start = index;
  while (start > 0 && inBlock(start - 1)) start--;
  let end = index;
  while (end < lines.length - 1 && inBlock(end + 1)) end++;
  return lines
    .slice(start, end + 1)
    .filter((line) => !isBlank(line) && !isComment(line))
    .map((line) => line.trim());
}

/** A declaration split into property and value; null for anything that is not one. */
function declaration(line: string): { prop: string; value: string } | null {
  const match = line.trim().match(/^([a-z-]+)\s*:\s*(.+?);?\s*$/);
  if (!match || match[1].startsWith("--")) return null;
  return { prop: match[1], value: match[2].trim() };
}

/** A spread-only shadow is a ring; anything with a blur or an offset is a shadow and not our business. */
const RING_SHADOW = /^(inset\s+)?0 0 0 \d+px\s+(\S+)$/;

/**
 * The colour a ring-drawing declaration names, or null when the declaration
 * draws no ring: `outline: none`, a drop shadow, a width or style alone.
 */
function ringColour({
  prop,
  value,
}: {
  prop: string;
  value: string;
}): string | null {
  if (/^(none|0|initial|inherit|unset|revert)$/.test(value)) return null;
  switch (prop) {
    case "outline": {
      const parts = value
        .split(/\s+/)
        .filter(
          (part) =>
            !/^\d/.test(part) &&
            !/^(auto|none|solid|dashed|dotted|double|groove|ridge|inset|outset|hidden|thin|medium|thick)$/.test(
              part,
            ),
        );
      return parts.length ? parts[parts.length - 1] : null;
    }
    case "outline-color":
    case "border-color":
      return value;
    case "box-shadow": {
      const ring = value.match(RING_SHADOW);
      return ring ? ring[2] : null;
    }
    default:
      return null;
  }
}

/** The block draws the field ring: a spread-only shadow of $link, so the outline it drops is replaced and not removed. */
const drawsFieldRing = (block: string[]): boolean =>
  block.some((line) => {
    const decl = declaration(line);
    if (!decl || decl.prop !== "box-shadow") return false;
    const ring = decl.value.match(RING_SHADOW);
    return ring !== null && RING_COLOUR.test(ring[2]);
  });

describe("the focus ring", () => {
  const hidden: string[] = [];
  const grey: string[] = [];
  const offColour: string[] = [];
  const files = collect(CLIENT_SRC);

  for (const file of files) {
    const path = where(file);
    const lines = readFileSync(file, "utf8").split("\n");
    lines.forEach((line, index) => {
      if (isBlank(line) || isComment(line)) return;
      const chain = selectorsAbove(lines, index);
      if (!chain.some((selector) => /focus/i.test(selector))) return;
      const at = `${path}:${index + 1} under "${chain[0]}"`;

      if (GREY.test(line)) grey.push(at);

      const decl = declaration(line);
      if (!decl) return;

      if (
        decl.prop === "outline" &&
        /^(none|0)$/.test(decl.value) &&
        !chain.some((selector) => /focus-visible/.test(selector)) &&
        !(path in ALLOWED_HIDE) &&
        !drawsFieldRing(blockAround(lines, index))
      ) {
        hidden.push(at);
      }

      const colour = ringColour(decl);
      if (colour === null || RING_COLOUR.test(colour)) return;
      if (ALLOWED_COLOUR[path]?.colour === colour) return;
      offColour.push(`${at}: ${decl.prop}: ${decl.value}`);
    });
  }

  it("reads the whole style layer", () => {
    expect(files.length).toBeGreaterThan(50);
  });

  it("is never taken away from the keyboard", () => {
    expect(hidden.sort()).toEqual([]);
  });

  it("is never the grey it used to be", () => {
    expect(grey.sort()).toEqual([]);
  });

  it("is drawn in $link and in nothing else", () => {
    expect(offColour.sort()).toEqual([]);
  });

  it("is declared globally for the block controls", () => {
    const reset = readFileSync(
      join(CLIENT_SRC, "assets/styles/Reset.sass"),
      "utf8",
    );
    for (const control of ["a", "button", "[tabindex]"]) {
      expect(reset).toContain(`${control}:focus-visible`);
    }
  });

  it("steps one pixel out on the main action, and nowhere else", () => {
    // The main action's fill is $link, the ring's colour: flush with the edge
    // the ring would vanish into it. +button-primary moves it out by the least
    // gap that shows the page between the two, for the page's button and the
    // guest card's alike. Any other control that stepped out would be a ring
    // drawn round a control rather than on it.
    const compiled = ruleMap(
      compileSass(
        [
          '@use "@/assets/styles/Inputs" as *',
          ".probe\n  +button\n  &.primary\n    +button-primary",
        ].join("\n"),
      ),
    );
    expect(compiled.get(".probe:focus-visible")).toMatchObject({
      outline: "2px solid var(--link)",
      "outline-offset": "0",
    });
    expect(compiled.get(".probe.primary:focus-visible")).toEqual({
      "outline-offset": "1px",
    });

    const stepsOut = files
      .filter((file) =>
        /outline-offset:\s*1px/.test(readFileSync(file, "utf8")),
      )
      .map(where);
    expect(stepsOut).toEqual(["assets/styles/Inputs.sass"]);
  });

  it("is the inline-text ring on every link-shaped button", () => {
    // A <button> that reads as a link is a word of the sentence it stands in,
    // and the global rule would give it the 2px box of a block control.
    const probes = [
      "+inline-link-button",
      "+muted-link-button",
      "+muted-link",
      "+link-tone($accent-green, $accent-green-hover)",
      "+expand-toggle-button",
    ];
    const compiled = ruleMap(
      compileSass(
        [
          '@use "@/assets/styles/Inputs" as *',
          ...probes.map((include, at) => `.probe-${at}\n  ${include}`),
        ].join("\n"),
      ),
    );
    probes.forEach((include, at) => {
      expect(compiled.get(`.probe-${at}:focus-visible`), include).toEqual({
        outline: "1px solid var(--link)",
        "outline-offset": "2px",
      });
    });
  });

  it("is declared for every field by the shared field mixin", () => {
    // InputsGlobal.sass applies @mixin input() to input, textarea and select
    // for the whole document, so the mixin (or one it includes) is the one
    // place the field ring can be given to all three — and the one place it
    // can go missing for all three at once, which is what happened before this
    // check existed. The ring is the recipe itself: the frame turns $link and a
    // 1px shadow of $link sits flush against it, on :focus.
    const lines = readFileSync(
      join(CLIENT_SRC, "assets/styles/Inputs.sass"),
      "utf8",
    ).split("\n");
    const mixinBody = (name: string): string[] => {
      const start = lines.findIndex((line) =>
        new RegExp(`^@mixin ${name}\\(`).test(line),
      );
      expect(start, `@mixin ${name}() is declared`).toBeGreaterThanOrEqual(0);
      let end = start + 1;
      while (
        end < lines.length &&
        (isBlank(lines[end]) || indentOf(lines[end]) > 0)
      )
        end++;
      return lines.slice(start + 1, end);
    };
    const own = mixinBody("input");
    const included = own
      .map((line) => line.trim().match(/^(?:\+|@include\s+)([\w-]+)/))
      .filter((match): match is RegExpMatchArray => match !== null)
      .flatMap((match) => mixinBody(match[1]));
    const body = [...own, ...included];
    const focus = body.findIndex((line) => /^\s*&:focus\s*$/.test(line));
    expect(focus, "the field mixin has a &:focus block").toBeGreaterThanOrEqual(
      0,
    );
    let first = focus + 1;
    while (first < body.length && isBlank(body[first])) first++;
    const block = blockAround(body, first);
    expect(block).toContain("border-color: $link");
    expect(block).toContain("box-shadow: 0 0 0 1px $link");
  });
});
