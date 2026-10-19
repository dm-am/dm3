import { readFileSync } from "node:fs";
import { join } from "node:path";

import { includeIgnoreFile } from "@eslint/compat";
import js from "@eslint/js";
import prettier from "@vue/eslint-config-prettier";
import {
  defineConfigWithVueTs,
  vueTsConfigs,
} from "@vue/eslint-config-typescript";
import boundaries from "eslint-plugin-boundaries";
import vue from "eslint-plugin-vue";
import vuejsAccessibility from "eslint-plugin-vuejs-accessibility";

import {
  FUNCTION_SCOPE,
  LIMITS,
  REGISTER,
  recordedRules,
} from "./src/gates/sizeRatchet.js";

/**
 * What `eslint .` walks. Flat config has no `--ext`: the extensions a bare
 * directory expands to are the ones some config block names, so this list is
 * that flag, kept in the one place the rules below also point at.
 */
const LINTED = ["**/*.{js,jsx,cjs,mjs,ts,tsx,cts,mts,vue}"];

/**
 * FSD layers, outermost first. A module may import from any layer below its own
 * and, within its own layer, only through another slice's `@x` door.
 * See docs/conventions/PATTERNS.md — this config is that document, enforced.
 */
const LAYERS = ["app", "pages", "widgets", "features", "entities", "shared"];

/** Every layer strictly below the given one, doors included. */
const below = (layer) =>
  LAYERS.slice(LAYERS.indexOf(layer) + 1).flatMap((lower) => [
    lower,
    `${lower}-x`,
  ]);

/** Layers made of slices. `app` is one composition root, `shared` is a kit. */
const SLICED = LAYERS.slice(1, -1);

/**
 * Layers whose slices publish through a barrel, and the file a consumer of one
 * may address.
 *
 * `pages` is deliberately absent and must stay absent: a page has exactly one
 * consumer, the router, and the router imports the page file itself. Routing
 * that through a barrel would pull every page into a single chunk and end code
 * splitting. No page directory has an index.ts, and none should.
 */
const BARRELED = ["widgets", "features", "entities"];
const SLICE_ENTRY = ["index.ts"];
const isBarreled = (type) => BARRELED.includes(type);

/** The native modals the design system replaces, with what to use instead. */
const NATIVE_MODALS = {
  confirm: "Use shared/ui/ConfirmDialog.",
  alert: "Use useToast().",
  prompt: "Use a dialog with a form field.",
};

/**
 * A cast of a cast, `x as unknown as T` foremost, in either spelling of a cast.
 * `as const` fixes the type of a literal and is no cast.
 */
const CAST_OF_A_CAST = {
  selector:
    ":matches(TSAsExpression, TSTypeAssertion) > :matches(TSAsExpression, TSTypeAssertion).expression:not([typeAnnotation.typeName.name='const'])",
  message:
    "A cast of a cast passes a value off as a type the compiler refused it: make the type of the value, or of its model, say what it holds.",
};

/**
 * Components the app registers globally, read out of the registration itself so
 * this config cannot drift from it: drop a `.component(...)` line and every
 * template that leaned on that global becomes a lint error instead of a runtime
 * console warning nobody reads.
 */
const GLOBAL_COMPONENTS = [
  ...readFileSync(
    join(import.meta.dirname, "src/app/providers/components.ts"),
    "utf8",
  ).matchAll(/\.component\(\s*"([^"]+)"/g),
].map(([, name]) => name);

if (GLOBAL_COMPONENTS.length === 0) {
  throw new Error(
    "src/app/providers/components.ts registers no component by literal name: the vue/no-undef-components whitelist cannot be derived from it, so update this config alongside the registration.",
  );
}

export default defineConfigWithVueTs(
  // Flat config has no `--ignore-path`, so .gitignore is read into `ignores`
  // rather than restated here: build output, coverage and reports stay out of
  // the lint run, and what is not ours to lint keeps one home.
  includeIgnoreFile(join(import.meta.dirname, ".gitignore")),
  { name: "dm/files-to-lint", files: LINTED },

  js.configs.recommended,
  vue.configs["flat/essential"],
  // `base` registers the plugin and the parser; `eslintRecommended` switches off
  // the core rules TypeScript already answers. Together they are what
  // `@vue/eslint-config-typescript` used to mean by default, and what this tree
  // has been linted against. Its `recommended` is a strictly larger set, the
  // whole typescript-eslint recommended preset: the shipped code takes
  // `no-explicit-any` of it (dm/shipped below), and turning on the rest is a
  // decision about the code, for whoever works through what it finds.
  vueTsConfigs.base,
  vueTsConfigs.eslintRecommended,
  prettier,

  {
    name: "dm/settings",
    files: LINTED,
    languageOptions: {
      ecmaVersion: "latest",
    },
    plugins: {
      boundaries,
      "vuejs-accessibility": vuejsAccessibility,
    },
    settings: {
      "boundaries/include": ["src/**/*.{ts,vue}"],
      // A dynamic import crosses a layer just as a static one does — the single
      // upward import this rule was written for hid behind exactly that form.
      "boundaries/dependency-nodes": ["import", "dynamic-import", "export"],
      "boundaries/elements": [
        // A slice's `@x` folder is its own element type, so importing that door
        // is allowed while importing the slice root from the same layer is not.
        // The trailing `.ts` is literal, so the second capture is the consumer's
        // slice name rather than a file name with its extension.
        ...SLICED.map((layer) => ({
          type: `${layer}-x`,
          pattern: `src/${layer}/*/@x/*.ts`,
          // `mode` is deprecated in v7; its replacement stops matching a door
          // FILE, which this element is. Revisit with v8.
          mode: "full",
          capture: ["slice", "consumer"],
        })),
        ...SLICED.map((layer) => ({
          type: layer,
          pattern: `src/${layer}/*`,
          capture: ["slice"],
        })),
        { type: "app", pattern: "src/app", partialMatch: false },
        { type: "shared", pattern: "src/shared", partialMatch: false },
      ],
      // `@/…` is read out of the tsconfig that declares it. One project only:
      // with a single project the resolver takes the path mapping and never
      // consults include/exclude, so the specs tsconfig.app.json excludes still
      // resolve.
      "import/resolver": {
        typescript: {
          project: "tsconfig.app.json",
          extensions: [".ts", ".js", ".vue", ".json"],
        },
      },
    },
    rules: {
      // The design system deliberately uses single-word names for shared UI
      // primitives (Button, Tooltip, Tabs, Form, Paging, Header, Footer, ...).
      "vue/multi-word-component-names": "off",

      // A leading underscore is the language-wide way to say "bound on purpose,
      // used on purpose nowhere" - the omit idiom `const { text: _text, ...rest }`
      // being the common case, where the binding exists so the field does not
      // travel on with the rest. Without this the only way to write that is a
      // disable comment above every occurrence, which states the same intent in a
      // form nothing can check and which hides the next, less innocent one.
      //
      // The core rule steps aside for the TypeScript-aware one: it cannot read a
      // type-only import or a declaration-merged name and reports both as dead.
      "no-unused-vars": "off",
      "@typescript-eslint/no-unused-vars": [
        "warn",
        {
          argsIgnorePattern: "^_",
          varsIgnorePattern: "^_",
          caughtErrorsIgnorePattern: "^_",
          ignoreRestSiblings: true,
        },
      ],

      // Accessibility, as a rule of the build rather than of a review. Named one
      // by one rather than through the plugin's preset: every member was
      // measured against this tree, and what is missing is missing for a reason
      // (below).
      "vuejs-accessibility/alt-text": "error",
      "vuejs-accessibility/anchor-has-content": "error",
      "vuejs-accessibility/aria-props": "error",
      "vuejs-accessibility/aria-role": "error",
      "vuejs-accessibility/aria-unsupported-elements": "error",
      "vuejs-accessibility/click-events-have-key-events": "error",
      // A field the template ties to no name at all. An `id` satisfies it (the
      // `for` at the other end is label-has-for's), and so does an `aria-label`
      // where the field has no caption of its own. The plugin reads `Select` as
      // the native `select`, so every use names its control at the call site.
      "vuejs-accessibility/form-control-has-label": "error",
      "vuejs-accessibility/heading-has-content": "error",
      "vuejs-accessibility/iframe-has-title": "error",
      // `some`, not the plugin's default `every`: FormField's label sits outside
      // the row it names, so no label both wraps its field and carries a `for`.
      "vuejs-accessibility/label-has-for": [
        "error",
        { required: { some: ["nesting", "id"] } },
      ],
      "vuejs-accessibility/media-has-caption": "error",
      "vuejs-accessibility/no-access-key": "error",
      "vuejs-accessibility/no-aria-hidden-on-focusable": "error",
      "vuejs-accessibility/no-autofocus": "error",
      "vuejs-accessibility/no-distracting-elements": "error",
      "vuejs-accessibility/no-redundant-roles": "error",
      "vuejs-accessibility/no-role-presentation-on-focusable": "error",
      "vuejs-accessibility/role-has-required-aria-props": "error",
      "vuejs-accessibility/tabindex-no-positive": "error",

      // Held back, and why.
      //
      // interactive-supports-focus: both role="option" sites are correct (focus
      // stays on the input with aria-activedescendant; the notepad's roster
      // uses a roving tabindex the rule cannot evaluate).
      //
      // no-onchange: jsx-a11y deprecated it, and @blur instead of @change would
      // lose the value on the way out.
      //
      // mouse-events-have-key-events and no-static-element-interactions are a
      // real backlog (hover-only affordances, clickable divs): a screen-by-screen
      // job, not a config line.

      // An unregistered tag is only a console warning at run time, so the
      // globals above and the router's own two are the only names a template may
      // use without importing them.
      "vue/no-undef-components": [
        "error",
        {
          ignorePatterns: [
            ...GLOBAL_COMPONENTS,
            "RouterLink",
            "RouterView",
          ].map((name) => `^${name}$`),
        },
      ],

      // The design system has one confirmation dialog (shared/ui/ConfirmDialog)
      // and one prompt, the editor's panel (BBCodeEditor/InsertPanel). Native
      // modals ignore the theme, block the event loop and are untestable. Both
      // spellings are banned: the bare global and the window property.
      "no-restricted-globals": [
        "error",
        ...Object.entries(NATIVE_MODALS).map(([name, message]) => ({
          name,
          message,
        })),
      ],
      "no-restricted-properties": [
        "error",
        ...Object.entries(NATIVE_MODALS).map(([property, message]) => ({
          object: "window",
          property,
          message,
        })),
      ],

      // Files outside src/ (config, e2e) are simply not FSD elements.
      "boundaries/no-unknown-files": "off",
      "boundaries/no-unknown": "off",
      "boundaries/dependencies": [
        "error",
        {
          default: "disallow",
          // `{{to.…}}`, not `{{target.…}}`: v7 renders the latter as an empty
          // string, so every message read "pages may not import :" and named
          // neither end of the edge it refused.
          message:
            "{{from.element.type}} may not import {{to.element.type}} this way: FSD allows downward imports only, into another slice through its index.ts barrel, and same-layer only through the target slice's @x door (docs/conventions/PATTERNS.md).",
          policies: [
            // Downward: any layer below, doors included - and into a sliced layer
            // only through that slice's own barrel (`fileInternalPath`).
            ...LAYERS.flatMap((layer) =>
              [layer, `${layer}-x`].flatMap((from) =>
                [
                  {
                    types: below(layer).filter(isBarreled),
                    entry: SLICE_ENTRY,
                  },
                  { types: below(layer).filter((t) => !isBarreled(t)) },
                ]
                  .filter(({ types }) => types.length > 0)
                  .map(({ types, entry }) => ({
                    from: { element: { type: from } },
                    allow: {
                      to: {
                        element: entry
                          ? { types: { anyOf: types }, fileInternalPath: entry }
                          : { types: { anyOf: types } },
                      },
                    },
                  })),
              ),
            ),
            // Same layer: only the door addressed to the importing slice.
            ...SLICED.map((layer) => ({
              from: { element: { type: layer } },
              allow: {
                to: {
                  element: {
                    type: `${layer}-x`,
                    captured: { consumer: "{{from.element.captured.slice}}" },
                  },
                },
              },
            })),
            // A door re-exports from the slice that owns it.
            ...SLICED.map((layer) => ({
              from: { element: { type: `${layer}-x` } },
              allow: {
                to: {
                  element: {
                    type: layer,
                    captured: { slice: "{{from.element.captured.slice}}" },
                  },
                },
              },
            })),
            // Neither app nor shared is sliced, so their internal folders are free
            // to reference each other.
            {
              from: { element: { type: "app" } },
              allow: { to: { element: { type: "app" } } },
            },
            {
              from: { element: { type: "shared" } },
              allow: { to: { element: { type: "shared" } } },
            },
          ],
        },
      ],
    },
  },

  // Code that does nothing. Each rule below found nothing when it was added, so
  // it is on to keep it that way: a finding is code to delete, never a line to
  // exempt. The rules of the family that did find something need a decision
  // about the code, and are not here.
  {
    name: "dm/dead-code",
    files: LINTED,
    rules: {
      // A ref="name" in a template that no script reads is an element named for
      // nobody: the script that held it is gone and the name stayed behind.
      "vue/no-unused-refs": "error",

      // "a" + "b" inside a template expression is one string written in two
      // pieces.
      "vue/no-useless-concat": "error",

      // A constructor that is empty or only forwards its arguments to super does
      // what the one it replaces does.
      "@typescript-eslint/no-useless-constructor": "error",

      // export {} in a file that already imports or exports something is a
      // leftover of the time the file had neither.
      "@typescript-eslint/no-useless-empty-export": "error",

      // {} as a type admits every value but null and undefined, which is rarely
      // what the author meant; the type that was meant has a name.
      "@typescript-eslint/no-empty-object-type": "error",

      // A statement that computes a value and drops it is a call that lost its
      // parentheses or a ternary written where an if was meant.
      "@typescript-eslint/no-unused-expressions": "error",

      // import { a as a } and { a: a } rename nothing.
      "no-useless-rename": "error",

      // { ["a"]: 1 } is { a: 1 }.
      "no-useless-computed-key": "error",

      // "a" + "b" between two literals is one literal.
      "no-useless-concat": "error",

      // A bare { } block that scopes no let, const or function only indents.
      "no-lone-blocks": "error",

      // x ? true : false is x, and x ? x : y is x || y.
      "no-unneeded-ternary": "error",

      // A label on a break or continue that already targets the loop it stands
      // in names nothing.
      "no-extra-label": "error",
    },
  },

  // The code the app ships, the files tsconfig.app.json compiles: the specs and
  // their harness hand over partial stand-ins on purpose.
  {
    name: "dm/shipped",
    files: ["src/**/*.{ts,vue}"],
    ignores: [
      "src/**/*.spec.ts",
      "src/**/*.fixtures.ts",
      "src/**/*.doubles.ts",
      "src/shared/lib/testing/**",
    ],
    rules: {
      // any switches the compiler off for the value and for all it reaches.
      "@typescript-eslint/no-explicit-any": "error",

      // The script and the template alike.
      "no-restricted-syntax": ["error", CAST_OF_A_CAST],
      "vue/no-restricted-syntax": ["error", CAST_OF_A_CAST],

      // The app tells the console a warning or an error; any other line is
      // debugging left behind.
      "no-console": ["error", { allow: ["warn", "error"] }],
    },
  },

  // Size: how much of a file, a component block and a function an agent has to
  // read at once. The limits and the files already past them live in
  // src/gates/sizeRatchet.js; the blocks below are the only place they reach the
  // linter, and src/gates/sizeRatchet.spec.ts checks that no file is held to
  // anything else.
  {
    name: "dm/size",
    files: LINTED,
    rules: {
      "max-lines": ["error", { max: LIMITS.lines }],
      complexity: ["error", LIMITS.complexity],
    },
  },
  {
    name: "dm/size/functions",
    files: FUNCTION_SCOPE,
    rules: {
      "max-lines-per-function": ["error", { max: LIMITS.functionLines }],
    },
  },
  {
    name: "dm/size/blocks",
    files: ["**/*.vue"],
    rules: { "vue/max-lines-per-block": ["error", { ...LIMITS.blocks }] },
  },
  ...Object.entries(REGISTER).map(([path, entry]) => ({
    name: `dm/size/recorded/${path}`,
    files: [path],
    rules: recordedRules(path, entry),
  })),
);
