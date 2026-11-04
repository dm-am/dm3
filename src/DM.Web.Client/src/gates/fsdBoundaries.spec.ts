/**
 * @vitest-environment node
 */

/**
 * The layer rules of PATTERNS.md are held by the linter, and this file holds
 * the linter to them by running it: the config is loaded rather than restated,
 * and the probes are the edges that must be refused plus the cases that must
 * keep passing. The controls matter as much: if the import resolver stops
 * resolving `@/...`, every "must be refused" assertion passes by seeing nothing.
 */
import { describe, it, expect } from "vitest";
import { existsSync, readFileSync } from "fs";
import { createRequire } from "module";
import { join, relative, resolve } from "path";
import { pathToFileURL } from "url";
import { CLIENT_SRC, collect } from "@/shared/lib/testing/sourceTree";

/** Every .ts/.vue under a directory, node_modules and build output aside. */
const isSource = (name: string): boolean =>
  name.endsWith(".ts") || name.endsWith(".vue");

// src -> DM.Web.Client
const CLIENT_ROOT = resolve(CLIENT_SRC, "..");
const CONFIG = join(CLIENT_ROOT, "eslint.config.js");

const requireFromConfig = createRequire(CONFIG);

interface FlatConfigBlock {
  settings?: Record<string, unknown>;
  rules?: Record<string, unknown>;
}

interface BoundariesBlock {
  settings: Record<string, unknown>;
  rules: Record<string, unknown>;
}

interface LintMessage {
  ruleId: string | null;
  message: string;
}

interface LinterLike {
  verify(
    code: string,
    config: unknown,
    options: { filename: string },
  ): LintMessage[];
}

/**
 * The config, evaluated rather than parsed. Flat config is an ES module whose
 * default export is the finished array of blocks, so it is imported and the one
 * block that carries the boundaries settings is picked out of it. Nothing below
 * restates a rule; if that block ever splits in two, this throws rather than
 * quietly lints against half a config.
 */
const blocks = (
  (await import(pathToFileURL(CONFIG).href)) as { default: FlatConfigBlock[] }
).default;

function boundariesBlock(all: FlatConfigBlock[]): BoundariesBlock {
  const block = all.find(
    (candidate) => candidate.settings?.["boundaries/elements"] !== undefined,
  );
  if (!block?.settings || !block.rules?.["boundaries/dependencies"]) {
    throw new Error(
      "eslint.config.js has no single block carrying both the boundaries settings and the boundaries/dependencies rule: this file would be linting against something other than the config CI runs.",
    );
  }
  return { settings: block.settings, rules: block.rules };
}

const config = boundariesBlock(blocks);

const { Linter } = requireFromConfig("eslint") as {
  Linter: new (options?: { cwd?: string }) => LinterLike;
};
const boundaries = requireFromConfig("eslint-plugin-boundaries") as {
  rules: Record<string, unknown>;
};

const linter = new Linter({ cwd: CLIENT_ROOT });

/** What the real rule says about one import from one place. */
function refusals(file: string, specifier: string): LintMessage[] {
  return linter
    .verify(
      `import x from "${specifier}";\nexport default x;\n`,
      [
        {
          // Flat config decides by pattern, and a block that matches nothing
          // makes the Linter answer "no matching configuration found" instead
          // of running the rule — a warning with no ruleId, which the filter
          // below would drop as silently as a clean file.
          files: ["**/*.ts", "**/*.vue"],
          plugins: { boundaries },
          languageOptions: { ecmaVersion: "latest", sourceType: "module" },
          settings: config.settings,
          rules: {
            "boundaries/dependencies": config.rules["boundaries/dependencies"],
          },
        },
      ],
      { filename: join(CLIENT_ROOT, file) },
    )
    .filter((message) => message.ruleId === "boundaries/dependencies");
}

describe("FSD boundaries, as the linter actually applies them", () => {
  // Without these two the file could pass by resolving nothing at all.
  describe("the harness is live", () => {
    it("refuses an upward import", () => {
      expect(
        refusals("src/shared/probe.ts", "@/pages/home/HomePage.vue"),
      ).toHaveLength(1);
    });

    it("refuses a same-layer import outside the door", () => {
      expect(refusals("src/entities/game/probe.ts", "@/entities/user")).toEqual(
        [expect.objectContaining({ ruleId: "boundaries/dependencies" })],
      );
    });
  });

  describe("deep imports past a slice barrel", () => {
    const deep: [string, string][] = [
      ["src/pages/home/probe.ts", "@/entities/user/model/communityStore"],
      ["src/pages/home/probe.ts", "@/entities/game/model/store"],
      ["src/widgets/header/probe.ts", "@/features/auth/ui/LoginForm.vue"],
      ["src/pages/home/probe.ts", "@/widgets/header/Header.vue"],
    ];

    for (const [file, specifier] of deep) {
      it(`refuses ${specifier}`, () => {
        expect(refusals(file, specifier)).toHaveLength(1);
      });
    }
  });

  // Addressing is the only property a door has: a slice enters through the file
  // that names it and through no other, and that is the whole of what the extra
  // four files buy. The allow-probe below cannot hold it — an allow-probe passes
  // by resolving nothing, so it would go on passing against a door addressed to
  // somebody else, or against a path that does not exist at all.
  it("refuses a door addressed to another slice", () => {
    expect(
      refusals("src/entities/game/probe.ts", "@/entities/user/@x/testimonial"),
    ).toHaveLength(1);
  });

  describe("what the barrel rule must not break", () => {
    const allowed: [string, string][] = [
      // The barrel itself.
      ["src/pages/home/probe.ts", "@/entities/game"],
      ["src/widgets/header/probe.ts", "@/features/auth"],
      // shared is a kit, not a set of slices: it is addressed by path.
      ["src/pages/home/probe.ts", "@/shared/ui/Button/Button.vue"],
      ["src/entities/game/probe.ts", "@/shared/lib/utils/keyedCache"],
      // The door, addressed to the slice that imports it.
      ["src/entities/game/probe.ts", "@/entities/user/@x/game"],
      // pages have no barrel on purpose: the router imports the file, and a
      // barrel here would collapse code splitting.
      ["src/app/providers/probe.ts", "@/pages/home/HomePage.vue"],
    ];

    for (const [file, specifier] of allowed) {
      it(`allows ${specifier}`, () => {
        expect(refusals(file, specifier)).toHaveLength(0);
      });
    }
  });

  it("names both ends of the edge it refuses", () => {
    // The template placeholder for the imported side is `to`; `target`
    // renders as an empty string.
    const [message] = refusals(
      "src/pages/home/probe.ts",
      "@/entities/game/model/store",
    );
    expect(message.message).toContain("pages");
    expect(message.message).toContain("entities");
    expect(message.message).toContain("docs/conventions/PATTERNS.md");
  });
});

/**
 * `shared` is a kit and not a layer of slices: a component of it is addressed
 * by its own path, and the kit root carries no barrel, because a kit barrel
 * makes every consumer pay for everything in it and gives one component two
 * addresses.
 */
describe("the shared UI kit", () => {
  it("has one address per component and no root barrel", () => {
    expect(
      existsSync(join(CLIENT_ROOT, "src/shared/ui/index.ts")),
      "a barrel at the kit root merges unrelated components into one module: it re-exports what a consumer did not ask for, and it competes with the per-component path the rest of the tree uses",
    ).toBe(false);

    const offenders: string[] = [];
    for (const file of collect(join(CLIENT_ROOT, "src"), isSource)) {
      if (!/from "@\/shared\/ui"/.test(readFileSync(file, "utf8"))) continue;
      offenders.push(relative(CLIENT_ROOT, file).split("\\").join("/"));
    }
    expect(offenders).toEqual([]);
  });
});

/**
 * A globally registered component is usable in any template without an import,
 * an edge neither `boundaries/dependencies` nor `vue/no-undef-components` sees.
 * Only `shared` may be registered globally: it sits below every layer, while a
 * component of a sliced layer turns global registration into a way around the
 * door. Asserted at the registration, the only place a global can be created.
 */
describe("global component registration", () => {
  /** Local binding -> module it came from, `import type` aside. */
  function importOrigins(code: string): Map<string, string> {
    const origins = new Map<string, string>();
    for (const [, clause, from] of code.matchAll(
      /^import\s+(?!type\s)([^;]+?)\s+from\s+"([^"]+)";/gm,
    )) {
      for (const part of clause.replace(/[{}]/g, " ").split(",")) {
        const local = part
          .trim()
          .split(/\s+as\s+/)
          .pop();
        if (local) origins.set(local, from);
      }
    }
    return origins;
  }

  it("registers primitives of shared and nothing else", () => {
    const source = readFileSync(
      join(CLIENT_ROOT, "src/app/providers/components.ts"),
      "utf8",
    );
    const origins = importOrigins(source);
    const registered = [
      ...source.matchAll(/\.component\(\s*"([^"]+)"\s*,\s*([\w$]+)\s*\)/g),
    ].map(([, name, local]) => [name, origins.get(local) ?? "?"] as const);

    expect(
      registered.length,
      "no registration call was found: this check and the whitelist eslint.config.js derives from the same file would both be reading nothing",
    ).toBeGreaterThan(0);

    expect(
      registered
        .filter(([, from]) => !from.startsWith("@/shared/"))
        .map(([name, from]) => `${name} <- ${from}`),
      "a global component of a sliced layer is a same-layer import no linter can refuse: open an @x door for the consumer and import it there",
    ).toEqual([]);
  });

  // vue-tsc knows a global only by the declaration of GlobalComponents: a
  // registration it lacks leaves every use of that tag unchecked.
  it("declares for the type check what it registers, and nothing else", () => {
    const source = readFileSync(
      join(CLIENT_ROOT, "src/app/providers/components.ts"),
      "utf8",
    );
    const registered = [
      ...source.matchAll(/\.component\(\s*"([^"]+)"\s*,\s*([\w$]+)\s*\)/g),
    ].map(([, name, local]) => `${name}: typeof ${local}`);
    const declaration =
      /interface GlobalComponents \{([^}]*)\}/.exec(source)?.[1] ?? "";
    const declared = [...declaration.matchAll(/(\w+): typeof ([\w$]+);/g)].map(
      ([, name, local]) => `${name}: typeof ${local}`,
    );

    expect(declared.length).toBeGreaterThan(0);
    expect(declared.sort()).toEqual(registered.sort());
  });
});

/**
 * A barrel of a slice breaks the kit rule just as well by re-exporting a
 * component of the kit under its own name, and then neither search for its
 * consumers finds the other half. Only `shared/ui` is checked, and only the
 * alias form: types and stores of `shared` are re-exported by slices on purpose.
 */
describe("slice barrels", () => {
  it("re-export no component of the shared UI kit", () => {
    const offenders: string[] = [];
    for (const layer of ["entities", "features", "widgets"]) {
      const root = join(CLIENT_ROOT, "src", layer);
      if (!existsSync(root)) continue;
      for (const file of collect(root, isSource)) {
        const path = relative(CLIENT_ROOT, file).split("\\").join("/");
        if (!path.endsWith("/index.ts")) continue;
        for (const [, from] of readFileSync(file, "utf8").matchAll(
          /^export\s[^;]*?\sfrom\s+"(@\/shared\/ui\/[^"]+)";/gm,
        )) {
          offenders.push(`${path} -> ${from}`);
        }
      }
    }

    expect(
      offenders,
      "a second address for a component of the kit: import it from @/shared/ui/<Name> where it is used and drop the re-export",
    ).toEqual([]);
  });
});

/**
 * The same rule one folder over: `shared/lib/composables` has no barrel, so a
 * composable has one address and a search for its consumers finds them all.
 * Not a bundle-size rule: the production build drops unused re-exports, which
 * buildChunks.spec.ts measures.
 */
describe("the shared composables", () => {
  it("have one address each and no folder barrel", () => {
    expect(
      existsSync(join(CLIENT_ROOT, "src/shared/lib/composables/index.ts")),
      "a barrel over the composables folder gives every composable a second address and competes with the per-composable path the rest of the tree uses: import each composable from its own module",
    ).toBe(false);

    const offenders: string[] = [];
    for (const file of collect(join(CLIENT_ROOT, "src"), isSource)) {
      const code = readFileSync(file, "utf8");
      if (!/"@\/shared\/lib\/composables"/.test(code)) continue;
      offenders.push(relative(CLIENT_ROOT, file).split("\\").join("/"));
    }
    expect(
      offenders,
      "the folder barrel is gone: address the composable by its own module path",
    ).toEqual([]);
  });
});
