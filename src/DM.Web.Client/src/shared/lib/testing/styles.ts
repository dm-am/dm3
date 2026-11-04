/**
 * A stylesheet of this project compiled the way the browser gets it: jsdom
 * applies no scoped style and vitest compiles none, so a rule is otherwise
 * invisible to a test. Spec-only, like everything in this directory.
 */
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { pathToFileURL } from "node:url";
import { compileString } from "sass";
import { compileStyle, parse as parseSfc } from "vue/compiler-sfc";
import { CLIENT_SRC } from "./sourceTree";

/** Resolves the "@/" alias for the sass importer. */
const aliasUrl = (url: string): URL | null =>
  url.startsWith("@/")
    ? pathToFileURL(resolve(CLIENT_SRC, url.slice(2)))
    : null;

const VITE_CONFIG = resolve(CLIENT_SRC, "..", "vite.config.ts");

/** One declaration block, property to value. */
export type Declarations = Record<string, string>;

/** One rule of the flat output, as it was emitted. */
export interface StyleRule {
  selector: string;
  declarations: Declarations;
}

let prelude: string | undefined;

/** The layer vite puts before every stylesheet, read out of vite.config.ts. */
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

/** Stylesheets main.ts imports, i.e. the ones emitted exactly once. */
export const GLOBAL_SHEETS = new Set(
  [
    ...readFileSync(resolve(CLIENT_SRC, "app/main.ts"), "utf8").matchAll(
      /^import "@\/(assets\/styles\/[\w.-]+)";$/gm,
    ),
  ].map((found) => found[1]),
);

/** Indented sass with the project's prelude and alias; `from` resolves its relative @use. */
export function compileSass(block: string, from?: string): string {
  return compileString(`${sassPrelude()}\n${block}`, {
    syntax: "indented",
    url: from ? pathToFileURL(from) : undefined,
    importers: [
      {
        findFileUrl: aliasUrl,
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
 * The flat output as rules in emitted order, nothing merged: a rule in @media
 * under its own selector, an at-rule with declarations (@font-face) dropped.
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
 * A component's stylesheet as the build compiles it, scoped under `scopeId`,
 * for which of two rules wins: the scope attribute weighs.
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
              findFileUrl: aliasUrl,
            },
          ],
        },
      });
      if (result.errors.length) throw result.errors[0];
      return result.code;
    })
    .join("\n");
}

/** The selector without :where(...), brackets counted. */
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
 * The weight of a selector as (ids, classes, types); :where() weighs nothing,
 * a pseudo-element is left out, :not() and :is() are not taken apart.
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
