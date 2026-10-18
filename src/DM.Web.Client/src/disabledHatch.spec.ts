/**
 * @vitest-environment node
 */

/**
 * A control out of reach says so in one picture across the site: half its
 * opacity and the pointer of plain text, and a diagonal hatch in its own ink
 * over anything that has a box (+disabled-hatch in Inputs.sass). The checkbox
 * and the solid button drew it; the link-styled button and the outlined pill
 * kept opacity alone, the field took a grey fill and dimmed nothing, and the
 * self-made controls dimmed to .3, .4 or .6: five pictures of one state, and
 * half of a control and a whole one are the same thing to a reader with
 * nothing beside it to compare (owner's standard, 27 September 2026).
 *
 * The variants are read from the compiled mixins, where the state is decided;
 * the components that draw a box of their own for a control that cannot carry
 * the layer (Select), or take no mixin (a segment), are read from their
 * sheets; and the rest of the tree is walked for a private opacity or the
 * forbidding pointer under a disabled selector.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";
import {
  compileSass,
  componentCss,
  ruleMap,
  type Declarations,
} from "@/shared/lib/testing/styles";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

const USE = '@use "@/assets/styles/Inputs" as *';

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

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (/\.(vue|sass|scss)$/.test(name)) {
      out.push(full);
    }
  }
  return out;
}

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
    // The grey fill a disabled field took instead of dimming is gone: it was
    // the one disabled state on the site that dimmed nothing.
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
    // .3, .4 and .6 each stood for the same state somewhere; the forbidding
    // pointer said it a sixth way. A layer that restores a dimmed wrapper's
    // child to 1 is not a dimming and is let through.
    const opacities: string[] = [];
    const pointers: string[] = [];
    let seen = 0;
    for (const file of collect(CLIENT_SRC)) {
      const path = relative(CLIENT_SRC, file).split("\\").join("/");
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
