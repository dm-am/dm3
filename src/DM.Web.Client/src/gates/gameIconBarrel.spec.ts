/**
 * @vitest-environment node
 */

/**
 * The registry of game icons (gameIcons.ts, tens of kilobytes of paths) is
 * reached only by the pages that draw an award or an achievement, all behind
 * lazy routes. The barrel shared/ui/Icon is imported across the site for
 * SvgIcon; once it exported GameIcon too, the bundler put the registry in a
 * shared chunk that the first visit preloads. So the barrel does not export
 * GameIcon, and no file imports GameIcon through it: a page takes
 * "@/shared/ui/Icon/GameIcon.vue" directly.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

const BARREL = "shared/ui/Icon";
const GAME_ICON = /\bGameIcon\b|\bgameIcons\b/;

const isSource = (name: string): boolean =>
  (name.endsWith(".ts") || name.endsWith(".vue")) && !name.endsWith(".d.ts");

/** Where an import specifier of a file points, as a path under src. */
function target(file: string, specifier: string): string | undefined {
  if (specifier.startsWith("@/")) return specifier.slice(2);
  if (!specifier.startsWith(".")) return undefined;
  return relative(CLIENT_SRC, resolve(dirname(file), specifier)).replace(
    /\\/g,
    "/",
  );
}

const isBarrel = (path: string | undefined): boolean =>
  path === BARREL ||
  path === `${BARREL}/index` ||
  path === `${BARREL}/index.ts`;

/**
 * The export statements of a module that hand on GameIcon or the registry,
 * read whole across lines: by a name in braces or after `export * as`, by a
 * declaration, or by the module an `export *` re-exports.
 */
function gameIconExports(source: string): string[] {
  return [
    ...source.matchAll(
      /\bexport\s+(?:type\s+)?(\*(?:\s+as\s+\w+)?|\{[^}]*\}|(?:default\s+)?(?:async\s+)?(?:const|let|var|function\*?|class)\s+\w+)(?:\s*from\s*["']([^"']+)["'])?/g,
    ),
  ]
    .filter(([, names, specifier]) =>
      GAME_ICON.test(`${names} ${specifier ?? ""}`),
    )
    .map(([statement]) => statement.replace(/\s+/g, " "));
}

/** The GameIcon imports of the given files that go through the barrel. */
function barrelImports(files: { file: string; source: string }[]): string[] {
  const found: string[] = [];
  for (const { file, source } of files) {
    for (const [statement, names, specifier] of source.matchAll(
      /import\s+(?:type\s+)?\{([^}]*)\}\s*from\s*["']([^"']+)["']/g,
    )) {
      if (isBarrel(target(file, specifier)) && GAME_ICON.test(names))
        found.push(`${asPath(file)}: ${statement.replace(/\s+/g, " ")}`);
    }
  }
  return found;
}

describe("the game icon registry", () => {
  it("is not exported by the barrel of shared/ui/Icon", () => {
    const barrel = readFileSync(join(CLIENT_SRC, BARREL, "index.ts"), "utf8");
    expect(
      gameIconExports(barrel),
      `${BARREL}/index.ts exports GameIcon: every page that imports SvgIcon from the barrel would carry the registry into a shared chunk the first visit preloads; drop the export and import "@/shared/ui/Icon/GameIcon.vue" directly`,
    ).toEqual([]);
  });

  it("is not imported through the barrel", () => {
    const files = collect(CLIENT_SRC, isSource).map((file) => ({
      file,
      source: readFileSync(file, "utf8"),
    }));
    expect(
      barrelImports(files),
      'import GameIcon from "@/shared/ui/Icon/GameIcon.vue", not from the barrel: the barrel rides in the first visit',
    ).toEqual([]);
    // The pages of awards and achievements still draw it, directly.
    expect(
      files.filter(
        ({ file, source }) =>
          !file.endsWith(".spec.ts") &&
          source.includes('from "@/shared/ui/Icon/GameIcon.vue"'),
      ).length,
    ).toBeGreaterThanOrEqual(5);
  });
});
