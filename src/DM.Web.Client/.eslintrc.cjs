/* eslint-env node */
require("@rushstack/eslint-patch/modern-module-resolution");

const fs = require("node:fs");
const path = require("node:path");

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
 * Components the app registers globally, read out of the registration itself so
 * this config cannot drift from it: drop a `.component(...)` line and every
 * template that leaned on that global becomes a lint error instead of a runtime
 * console warning nobody reads.
 */
const GLOBAL_COMPONENTS = [
  ...fs
    .readFileSync(
      path.resolve(__dirname, "src/app/providers/components.ts"),
      "utf8",
    )
    .matchAll(/\.component\(\s*"([^"]+)"/g),
].map(([, name]) => name);

if (GLOBAL_COMPONENTS.length === 0) {
  throw new Error(
    "src/app/providers/components.ts registers no component by literal name: the vue/no-undef-components whitelist cannot be derived from it, so update this config alongside the registration.",
  );
}

module.exports = {
  root: true,
  extends: [
    "plugin:vue/vue3-essential",
    "eslint:recommended",
    "@vue/eslint-config-typescript",
    "@vue/eslint-config-prettier",
  ],
  plugins: ["boundaries"],
  parserOptions: {
    ecmaVersion: "latest",
  },
  settings: {
    "boundaries/include": ["src/**/*.{ts,vue}"],
    // A dynamic import crosses a layer just as a static one does — the single
    // upward import this rule was written for hid behind exactly that form.
    "boundaries/dependency-nodes": ["import", "dynamic-import", "export"],
    "boundaries/elements": [
      // A slice's `@x` folder is its own element type, so importing that door is
      // allowed while importing the slice root from the same layer is not. The
      // trailing `.ts` is literal, so the second capture is the consumer's slice
      // name rather than a file name with its extension.
      ...SLICED.map((layer) => ({
        type: `${layer}-x`,
        pattern: `src/${layer}/*/@x/*.ts`,
        // `mode` is deprecated in v7, but its replacement (partialMatch: false)
        // carries folder semantics and stops matching a door FILE, which is
        // exactly what this element is. Revisit when v8 offers a file-shaped
        // descriptor; until then the deprecation notice is the honest cost.
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
    "import/resolver": {
      alias: {
        map: [["@", "./src"]],
        extensions: [".ts", ".js", ".vue", ".json"],
      },
    },
  },
  rules: {
    // The design system deliberately uses single-word names for shared UI
    // primitives (Button, Tooltip, Tabs, Form, Paging, Header, Footer, ...).
    "vue/multi-word-component-names": "off",

    // Nothing checked that a template's components resolve. Vue answers an
    // unregistered tag with a console warning, so lint, type-check and build
    // all stay green while a page renders without its heading, and a component
    // copied from a file that leaned on a global fails only at runtime. The
    // globals above and the router's own two are the only names a template may
    // use without importing them.
    "vue/no-undef-components": [
      "error",
      {
        ignorePatterns: [...GLOBAL_COMPONENTS, "RouterLink", "RouterView"].map(
          (name) => `^${name}$`,
        ),
      },
    ],

    // The design system has one confirmation dialog (shared/ui/ConfirmDialog)
    // and one prompt (BBCodeEditor/InputDialog). Native modals ignore the
    // theme, cannot be styled, block the event loop and are untestable — seven
    // destructive actions had drifted back onto them, including two halves of
    // one copy-pasted screen. Both spellings are banned: the bare global and
    // the window property.
    "no-restricted-globals": [
      "error",
      { name: "confirm", message: "Use shared/ui/ConfirmDialog." },
      { name: "alert", message: "Use useToast()." },
      { name: "prompt", message: "Use a dialog with a form field." },
    ],
    "no-restricted-properties": [
      "error",
      {
        object: "window",
        property: "confirm",
        message: "Use shared/ui/ConfirmDialog.",
      },
      { object: "window", property: "alert", message: "Use useToast()." },
      {
        object: "window",
        property: "prompt",
        message: "Use a dialog with a form field.",
      },
    ],

    // Files outside src/ (config, e2e) are simply not FSD elements.
    "boundaries/no-unknown-files": "off",
    "boundaries/no-unknown": "off",
    "boundaries/dependencies": [
      "error",
      {
        default: "disallow",
        message:
          "{{from.element.type}} may not import {{target.element.type}}: FSD allows downward imports only, and same-layer only through the target slice's @x door (docs/conventions/PATTERNS.md).",
        policies: [
          // Downward: any layer below, doors included.
          ...LAYERS.flatMap((layer) =>
            [layer, `${layer}-x`].map((from) => ({
              from: { element: { type: from } },
              allow: { to: { element: { types: { anyOf: below(layer) } } } },
            })),
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
};
