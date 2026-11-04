/**
 * @vitest-environment node
 */

/**
 * A control out of reach says so in one picture across the site: half its
 * opacity and the pointer of plain text, and a diagonal hatch in its own ink
 * over anything that has a box (+disabled-hatch in _Buttons.sass). The
 * variants are read from the compiled mixins, where the state is decided;
 * Select, which draws a box of its own, and a segment, which takes no mixin,
 * from their sheets; and the rest of the tree is walked for a private opacity
 * or the forbidding pointer under a disabled selector.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { join } from "path";
import {
  compileSass,
  componentCss,
  ruleMap,
  type Declarations,
} from "@/shared/lib/testing/styles";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

const USE = [
  '@use "@/assets/styles/Inputs" as *',
  '@use "@/assets/styles/Buttons" as *',
  '@use "@/assets/styles/Links" as *',
].join("\n");

/** Every variant with a box that can be disabled, as a probe class carrying it. */
const BOXED: Record<string, string> = {
  checkbox: "+checkbox",
  radio: "+radio",
  button: "+button",
  "button-link": "+button-link",
};

/** Controls without a box of their own: dimmed, never hatched. */
const UNBOXED: Record<string, string> = {
  input: "+input",
  "inline-link-button": "+inline-link-button",
  "muted-link-button": "+muted-link-button",
  "link-tone": "+link-tone($text-muted)",
};

const rules = ruleMap(
  compileSass(
    [
      USE,
      ...Object.entries({ ...BOXED, ...UNBOXED }).map(
        ([name, include]) => `.probe-${name}\n  ${include}`,
      ),
      // The main action is a modifier over the button, and a disabled one
      // keeps its fill under the hatch: the state is the button's.
      ".probe-primary\n  +button\n  &.primary\n    +button-primary",
      // A wrapper that draws the state for a control inside it.
      '.probe-wrapper\n  +disabled-hatch(".disabled")',
    ].join("\n"),
  ),
);

/** The two layers of the disabled state of one variant. */
const disabledOf = (
  selector: string,
): { control?: Declarations; hatch?: Declarations } => ({
  control: rules.get(`${selector}:disabled`) ?? rules.get(selector),
  hatch:
    rules.get(`${selector}:disabled::before`) ??
    rules.get(`${selector}::before`),
});

const isSheet = (name: string): boolean => /\.(vue|sass|scss)$/.test(name);

const indentOf = (line: string): number => line.match(/^\s*/)![0].length;
const isComment = (line: string): boolean => /^\s*(\/\/|\/\*|\*)/.test(line);
const isBlank = (line: string): boolean => !line.trim();

/** The selectors a declaration sits under, innermost first. */
function selectorsAbove(lines: string[], index: number): string[] {
  const chain: string[] = [];
  let indent = indentOf(lines[index]);
  for (let i = index - 1; i >= 0; i--) {
    if (isBlank(lines[i]) || isComment(lines[i])) continue;
    const own = indentOf(lines[i]);
    if (own >= indent) continue;
    chain.push(lines[i].trim());
    indent = own;
    if (own === 0) break;
  }
  return chain;
}

/** The style part of a source: a .vue file's <style> blocks, a sheet whole. */
const styleLines = (file: string): string[] => {
  const source = readFileSync(file, "utf8");
  if (!file.endsWith(".vue")) return source.split("\n");
  return [...source.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)]
    .map((found) => found[1])
    .join("\n")
    .split("\n");
};

describe("a disabled control", () => {
  // The yardstick: the state the checkbox has always drawn.
  const reference = disabledOf(".probe-checkbox");

  it("is drawn by the checkbox as half its opacity under a hatch", () => {
    expect(reference.control?.opacity).toBe("0.5");
    expect(reference.control?.cursor).toBe("default");
    expect(reference.hatch?.["mask-image"]).toContain("svg");
    expect(reference.hatch?.["background-color"]).toBe("currentColor");
  });

  it.each(["radio", "button", "button-link"])(
    "is drawn by %s the same way",
    (name) => {
      const { control, hatch } = disabledOf(`.probe-${name}`);

      expect(control?.opacity).toBe(reference.control?.opacity);
      expect(control?.cursor).toBe(reference.control?.cursor);
      expect(hatch).toEqual(reference.hatch);
    },
  );

  it("keeps the main action's fill under the button's hatch", () => {
    // Nothing of the primary's own speaks for the state: a disabled main
    // action stays pale blue under the same two layers, so a row of two still
    // says which of them submits.
    expect(rules.has(".probe-primary.primary:disabled")).toBe(false);
    expect(disabledOf(".probe-primary").hatch).toEqual(reference.hatch);
  });

  it("is drawn the same way by a wrapper that says it with a class", () => {
    const { control, hatch } = disabledOf(".probe-wrapper.disabled");
    expect(control?.opacity).toBe(reference.control?.opacity);
    expect(hatch).toEqual(reference.hatch);
  });

  it.each(Object.keys(UNBOXED))(
    "is dimmed by %s to the same opacity, with no hatch where there is no box",
    (name) => {
      const { control, hatch } = disabledOf(`.probe-${name}`);
      expect(control?.opacity).toBe(reference.control?.opacity);
      expect(control?.cursor).toBe("default");
      expect(rules.has(`.probe-${name}:disabled::before`)).toBe(false);
      expect(hatch).toBeUndefined();
    },
  );

  it("is a field that keeps its own fill", () => {
    // A grey fill instead of the dimming is a second picture of the state.
    expect(rules.get(".probe-input:disabled")?.["background-color"]).toBe(
      undefined,
    );
  });
});

describe("a component that draws the box of its control", () => {
  it("hatches the whole list, chevron included, and dims it once", () => {
    const sheet = ruleMap(
      componentCss(join(CLIENT_SRC, "shared/ui/Select/Select.vue")),
    );
    const reference = disabledOf(".probe-checkbox");
    expect(sheet.get(".select-control.disabled")?.opacity).toBe("0.5");
    expect(sheet.get(".select-control.disabled::before")).toEqual(
      reference.hatch,
    );
    // Dimmed by the field base as well, the list would stand at a quarter.
    expect(sheet.get(".select-input:disabled")?.opacity).toBe("1");
  });

  it("hatches a segment of the segmented control", () => {
    const sheet = ruleMap(
      componentCss(
        join(CLIENT_SRC, "shared/ui/SegmentedControl/SegmentedControl.vue"),
      ),
    );
    expect(sheet.get(".segment:disabled")?.opacity).toBe("0.5");
    expect(sheet.get(".segment:disabled::before")).toEqual(
      disabledOf(".probe-checkbox").hatch,
    );
  });
});

describe("the tree", () => {
  it("dims nothing out of reach to an opacity of its own", () => {
    // One opacity for the state. A layer that restores a dimmed wrapper's
    // child to 1 is not a dimming and is let through.
    const opacities: string[] = [];
    const pointers: string[] = [];
    let seen = 0;
    for (const file of collect(CLIENT_SRC, isSheet)) {
      const path = asPath(file);
      const lines = styleLines(file);
      lines.forEach((line, index) => {
        if (isBlank(line) || isComment(line)) return;
        const text = line.trim();
        if (/^cursor:\s*not-allowed\b/.test(text)) {
          pointers.push(`${path}: ${text}`);
          return;
        }
        const opacity = /^opacity:\s*(.+)$/.exec(text);
        if (!opacity) return;
        const chain = selectorsAbove(lines, index);
        const under = chain.find((one) => !one.startsWith("&::"));
        if (!under || !/disabled/.test(under)) return;
        seen += 1;
        const value = opacity[1].trim();
        if (value === "$disabled-opacity" || value === "1") return;
        opacities.push(`${path}: ${under} opacity: ${value}`);
      });
    }
    // The walk reaches the sources: the site's own states are found.
    expect(seen).toBeGreaterThan(3);
    expect(opacities).toEqual([]);
    expect(pointers).toEqual([]);
  });
});
