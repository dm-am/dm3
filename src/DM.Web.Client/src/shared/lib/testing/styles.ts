/**
 * A stylesheet of this project, compiled the way the browser gets it.
 *
 * jsdom applies no scoped styles and vitest never compiles a <style> block, so
 * a defect that lives in a rule is invisible to an ordinary mount: every
 * assertion about it ends up being an assertion about markup, and markup is
 * not where it went wrong. Compiling the block turns the rule into something a
 * test can read.
 *
 * Spec-only: it reads sources off disk and pulls in the sass compiler, neither
 * of which belongs in the browser bundle, and tsconfig.app.json excludes this
 * directory for that reason. It sits in shared because every layer may import
 * shared, and the specs that need it stand in three different ones.
 */
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { compileString } from "sass";
import { compileStyle, parse as parseSfc } from "vue/compiler-sfc";

const HERE = dirname(fileURLToPath(import.meta.url));

/** The directory the "@/" alias stands for. */
const CLIENT_SRC = resolve(HERE, "../../..");

const VITE_CONFIG = resolve(CLIENT_SRC, "..", "vite.config.ts");

/** One declaration block, property to value. */
export type Declarations = Record<string, string>;

/** One rule of the flat output, as it was emitted. */
export interface StyleRule {
  selector: string;
  declarations: Declarations;
}

let prelude: string | undefined;

/**
 * The shared layer vite puts in front of every stylesheet the project
 * compiles, read out of vite.config.ts rather than restated here.
 *
 * One source and not two, because a copy is only right on the day it is
 * written. The two copies this replaced had stopped at Variables and Themes,
 * so the first style block to reach for +card or a breakpoint would have
 * failed to compile in the test while building perfectly well in the app -
 * a spec that goes red over its own scaffolding teaches the reader to
 * switch it off.
 */
export function sassPrelude(): string {
  if (prelude !== undefined) return prelude;
  const declared = readFileSync(VITE_CONFIG, "utf8").match(
    /additionalData:\s*`([\s\S]*?)`/,
  )?.[1];
  if (!declared) {
    throw new Error(
      "vite.config.ts declares no additionalData template literal: the prelude that every style block of this project compiles against cannot be read from it.",
    );
  }
  prelude = declared;
  return prelude;
}

/**
 * Indented sass, compiled against the project's own prelude and alias. With
 * `from`, the block is compiled as that file, so its own relative @use lines
 * resolve the way they do in the build.
 */
export function compileSass(block: string, from?: string): string {
  return compileString(`${sassPrelude()}\n${block}`, {
    syntax: "indented",
    url: from ? pathToFileURL(from) : undefined,
    importers: [
      {
        findFileUrl: (url) =>
          url.startsWith("@/")
            ? pathToFileURL(resolve(CLIENT_SRC, url.slice(2)))
            : null,
      },
    ],
  }).css;
}

/** Every <style lang="sass"> block of a single file component, in order. */
export function sassBlocks(sfcPath: string): string {
  const blocks = [
    ...readFileSync(sfcPath, "utf8").matchAll(
      /<style[^>]*lang="sass"[^>]*>([\s\S]*?)<\/style>/g,
    ),
  ].map(([, block]) => block);
  if (blocks.length === 0) throw new Error(`${sfcPath} has no sass block`);
  return blocks.join("\n");
}

/** A component's own stylesheet, compiled. */
export function componentCss(sfcPath: string): string {
  return compileSass(sassBlocks(sfcPath));
}

/**
 * The flat output as a list of rules, in the order they were emitted.
 *
 * Nothing is merged: two rules for one selector stay two entries, which is
 * what tells a stylesheet that says a thing once from one that says it twice.
 * A rule nested in a @media comes out under its own selector, because the
 * condition is not part of what these tests ask about; an at-rule that carries
 * declarations of its own (@font-face) is dropped.
 */
export function styleRules(css: string): StyleRule[] {
  const found: StyleRule[] = [];
  for (const [, rawSelector, body] of css.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    const selector = rawSelector.trim().replace(/\s+/g, " ");
    if (selector.startsWith("@")) continue;
    const declarations: Declarations = {};
    for (const line of body.split(";")) {
      const colon = line.indexOf(":");
      if (colon > 0) {
        declarations[line.slice(0, colon).trim()] = line
          .slice(colon + 1)
          .trim();
      }
    }
    found.push({ selector, declarations });
  }
  return found;
}

/**
 * The same output as a selector-to-declarations map, repeated selectors merged
 * the way the cascade merges them: the later declaration wins.
 */
export function ruleMap(css: string): Map<string, Declarations> {
  const out = new Map<string, Declarations>();
  for (const { selector, declarations } of styleRules(css)) {
    out.set(selector, { ...out.get(selector), ...declarations });
  }
  return out;
}

/**
 * A component's stylesheet compiled the way the build compiles it: scoped
 * blocks rewritten under `scopeId` by the same compiler vite runs, unscoped
 * blocks left global. What componentCss cannot say is which of two rules wins,
 * because the scope attribute is part of the weight of a selector and
 * componentCss leaves it out; this output carries it.
 */
export function scopedComponentCss(sfcPath: string, scopeId: string): string {
  const { descriptor } = parseSfc(readFileSync(sfcPath, "utf8"), {
    filename: sfcPath,
  });
  return descriptor.styles
    .filter((style) => style.lang === "sass")
    .map((style) => {
      const result = compileStyle({
        source: style.content,
        filename: sfcPath,
        id: scopeId,
        scoped: style.scoped === true,
        preprocessLang: "sass",
        preprocessOptions: {
          // compiler-sfc asks for the indented syntax the legacy way
          // (`indentedSyntax`), which the modern sass API ignores.
          syntax: "indented",
          additionalData: sassPrelude(),
          importers: [
            {
              findFileUrl: (url: string) =>
                url.startsWith("@/")
                  ? pathToFileURL(resolve(CLIENT_SRC, url.slice(2)))
                  : null,
            },
          ],
        },
      });
      if (result.errors.length) throw result.errors[0];
      return result.code;
    })
    .join("\n");
}

/**
 * The selector with every :where(...) cut out, brackets counted rather than
 * matched by a pattern: a :where holding :not(.a):not(.b) closes on its own
 * last bracket, not on the first one inside it.
 */
function withoutWhere(selector: string): string {
  let out = "";
  let i = 0;
  while (i < selector.length) {
    if (selector.startsWith(":where(", i)) {
      let depth = 0;
      let j = i + ":where".length;
      for (; j < selector.length; j += 1) {
        if (selector[j] === "(") depth += 1;
        else if (selector[j] === ")") {
          depth -= 1;
          if (depth === 0) break;
        }
      }
      i = j + 1;
    } else {
      out += selector[i];
      i += 1;
    }
  }
  return out;
}

/**
 * The weight of a selector as (ids, classes, types), for the selectors these
 * stylesheets emit: classes, attributes (the scope id among them), pseudo-
 * classes and type names. :where() weighs nothing and a pseudo-element is left
 * out, as the cascade does; :not() and :is() are not taken apart.
 */
export function specificity(selector: string): [number, number, number] {
  const plain = withoutWhere(selector).replace(/::[\w-]+/g, "");
  const count = (pattern: RegExp) => (plain.match(pattern) ?? []).length;
  return [
    count(/#[\w-]+/g),
    count(/\.[\w-]+|\[[^\]]*\]|:[\w-]+/g),
    count(/(?:^|[\s>+~])[a-z][\w-]*/gi),
  ];
}

/** Whether the first weight outranks the second. */
export function outranks(
  a: [number, number, number],
  b: [number, number, number],
): boolean {
  for (let i = 0; i < 3; i += 1) if (a[i] !== b[i]) return a[i] > b[i];
  return false;
}
