/**
 * @vitest-environment node
 */

/**
 * The compiler the style specs are written against.
 *
 * It replaced two hand-made copies of itself, and both carried the same fault:
 * a prelude of two modules where vite injects five. Variables and Themes cover
 * a colour and a spacing step, which is all either spec happened to touch, so
 * nothing was red - and the first +card or breakpoint added to either
 * component would have taken the spec down with a compile error that says
 * nothing about the component. The probe below is that block, and it is the
 * whole reason the prelude is read out of vite.config.ts instead of typed out
 * again here.
 */
import { describe, it, expect } from "vitest";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { compileString } from "sass";
import {
  compileSass,
  componentCss,
  outranks,
  ruleMap,
  sassBlocks,
  sassPrelude,
  scopedComponentCss,
  specificity,
  styleRules,
} from "./styles";

const CLIENT_SRC = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");

/**
 * A block that asks each of the five injected modules for something: a mixin
 * from Surfaces, a breakpoint from Breakpoints, a height from Layout, a colour
 * from Themes and a spacing step from Variables.
 */
const PROBE = [
  ".probe",
  "  +card",
  "  min-height: $header-height",
  "  color: $text",
  "  @media (max-width: $bp-mobile)",
  "    padding: $small",
].join("\n");

describe("the style compiler the specs share", () => {
  it("compiles a block that reaches every module vite injects", () => {
    const css = compileSass(PROBE);
    const probe = styleRules(css).filter(({ selector }) =>
      selector.startsWith(".probe"),
    );

    // The base rule and the one inside the media query.
    expect(probe).toHaveLength(2);
    // +card came from Surfaces (its frame; a card is square and has no
    // radius to show for it), the height from Layout.
    expect(probe[0].declarations.border).toBe("1px solid var(--border)");
    expect(probe[0].declarations["min-height"]).toBe("100px");
    // The breakpoint resolved to a length rather than travelling as a name.
    expect(css).toMatch(/@media \(max-width: \d+px\)/);
  });

  it("goes red on that block with the two-module prelude the copies carried", () => {
    // The control. Without it the test above only says that some prelude
    // compiles the probe, not that the short one could not.
    const short = [
      '@use "@/assets/styles/Variables" as *',
      '@use "@/assets/styles/Themes" as *',
    ].join("\n");
    expect(() =>
      compileString(`${short}\n${PROBE}`, {
        syntax: "indented",
        importers: [
          {
            findFileUrl: (url) =>
              url.startsWith("@/")
                ? pathToFileURL(resolve(CLIENT_SRC, url.slice(2)))
                : null,
          },
        ],
      }),
    ).toThrow(/Undefined mixin/);
  });

  it("reads the prelude out of the config that the app builds with", () => {
    // Every module named there has to reach the block, so each one is a @use
    // line and there is nothing else in it to emit a rule.
    expect(sassPrelude()).toMatch(/@use "@\/assets\/styles\/Surfaces" as \*/);
    expect(compileSass("")).toBe("");
  });

  it("keeps a repeated selector as two rules, and merges it in the map", () => {
    // A page that restates a rule the partial already emits is the defect
    // messageToolbarStyles.spec.ts watches for, and a map would hide it.
    const css = compileSass(
      [".twice", "  color: $text", ".twice", "  color: $link"].join("\n"),
    );

    expect(styleRules(css)).toHaveLength(2);
    expect(ruleMap(css).size).toBe(1);
    expect(ruleMap(css).get(".twice")!.color).toBe(
      styleRules(css)[1].declarations.color,
    );
  });

  it("reads a component's own block off disk", () => {
    const drawer = resolve(CLIENT_SRC, "shared/ui/Drawer/MobileDrawer.vue");

    expect(sassBlocks(drawer)).toContain(".drawer-panel");
    expect(ruleMap(componentCss(drawer)).get(".drawer-content")).toBeDefined();
  });

  it("compiles a block as its own file, so a relative @use resolves", () => {
    const sheet = resolve(CLIENT_SRC, "assets/styles/AuthSurface.sass");
    const probe = ['@use "Inputs" as *', ".probe", "  +input-base"].join("\n");

    expect(() => compileSass(probe)).toThrow();
    expect(ruleMap(compileSass(probe, sheet)).get(".probe")).toBeDefined();
  });

  it("scopes a scoped block the way the build does, and leaves a global one", () => {
    const button = resolve(CLIENT_SRC, "shared/ui/Button/Button.vue");
    const dialog = resolve(CLIENT_SRC, "shared/ui/Layout/Dialog.vue");

    expect(
      ruleMap(scopedComponentCss(button, "data-v-b")).has("button[data-v-b]"),
    ).toBe(true);
    expect(ruleMap(scopedComponentCss(dialog, "data-v-d")).has(".dialog")).toBe(
      true,
    );
  });

  it("weighs a selector as the cascade does", () => {
    expect(specificity(".a .b[data-v-x] > *")).toEqual([0, 3, 0]);
    expect(specificity("button[data-v-x]")).toEqual([0, 1, 1]);
    expect(specificity(".a:where(:not(.b)) input:focus")).toEqual([0, 2, 1]);
    // A :where holding brackets of its own weighs nothing all the way to its
    // last bracket.
    expect(
      specificity(
        '.form-field--dialog .form-field-row[data-v-field] > input:where(:not([type="checkbox"]):not([type="radio"]))',
      ),
    ).toEqual([0, 3, 1]);
    expect(specificity("#id .a::before")).toEqual([1, 1, 0]);
    expect(outranks([0, 3, 0], [0, 1, 1])).toBe(true);
    expect(outranks([0, 1, 1], [0, 1, 1])).toBe(false);
  });
});
