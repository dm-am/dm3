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
  plugins: ["boundaries", "vuejs-accessibility"],
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

    // Accessibility, as a rule of the build rather than of a review.
    //
    // Until this block there was nothing: `plugin:vue/vue3-essential` says
    // nothing about accessibility, and the only automated check on the site was
    // one e2e spec measuring one tooltip. A <div> that answers a click and not
    // a key, an <img> with no alt, an aria-* attribute misspelt into silence —
    // all of it type-checks, lints, builds and renders, and the defect is
    // invisible to anyone holding a mouse and looking at the screen. Every
    // instance found by hand came back in the next screen written from the same
    // template.
    //
    // Named one by one rather than through the plugin's recommended preset. A
    // preset is a set someone switches off on the first red build; a list is a
    // set whose every member was measured against this tree. What is missing
    // from it is missing for a reason, and the reasons are below.
    "vuejs-accessibility/alt-text": "error",
    "vuejs-accessibility/anchor-has-content": "error",
    "vuejs-accessibility/aria-props": "error",
    "vuejs-accessibility/aria-role": "error",
    "vuejs-accessibility/aria-unsupported-elements": "error",
    "vuejs-accessibility/click-events-have-key-events": "error",
    "vuejs-accessibility/heading-has-content": "error",
    "vuejs-accessibility/iframe-has-title": "error",
    "vuejs-accessibility/media-has-caption": "error",
    "vuejs-accessibility/no-access-key": "error",
    "vuejs-accessibility/no-aria-hidden-on-focusable": "error",
    "vuejs-accessibility/no-autofocus": "error",
    "vuejs-accessibility/no-distracting-elements": "error",
    "vuejs-accessibility/no-redundant-roles": "error",
    "vuejs-accessibility/no-role-presentation-on-focusable": "error",
    "vuejs-accessibility/role-has-required-aria-props": "error",
    "vuejs-accessibility/tabindex-no-positive": "error",

    // Held back, and why. Each of these was run over the whole tree before
    // being left out, and the count is what it produced.
    //
    // label-has-for (76 in 31 files) and form-control-has-label (83 in 47) read
    // the association between a label and its control out of the TEMPLATE.
    // Where a form is built out of FormField (shared/ui/Form) it is not written
    // there: the component generates the id, finds the single labelable control
    // of its row after mount and wires `for`, `aria-describedby` and
    // `aria-invalid` onto it, which is what lets a caller drop any control into
    // the slot without repeating the plumbing. That part is covered by
    // FormField.spec.ts and the rules cannot see it.
    //
    // That is not most of what they report, and saying it was is how these two
    // came to be written off as false alarms. Measured over the tree: 56 of the
    // 76 and 31 of the 83 are in files that do not use FormField at all — a
    // <label> with no `for` beside an <input> with no id, in the award series
    // screen, the poll editor, the game room, the game post, both range pickers
    // and the topic view. Those are the defect the rules exist for, they are
    // invisible to CI today, and a screen reader reaching one of them announces
    // an unnamed field.
    //
    // Held back for the same reason as the two below, then: a red build nobody
    // can make green is how a11y linting gets switched off. The way in is the
    // primitives first (PasswordInput, TextArea, Select), then the seven screens
    // above, then the rule.
    //
    // interactive-supports-focus (2) wants every role="option" focusable. Both
    // sites are correct as they stand: the autocomplete keeps focus on the
    // input and moves the selection with aria-activedescendant, and the
    // notepad's roster uses a roving tabindex the rule cannot evaluate because
    // it is a binding.
    //
    // no-onchange (1) objects to @change on a native <select>. jsx-a11y
    // deprecated the same rule: the behaviour it protected against (VoiceOver
    // submitting on arrow keys) is a decade gone, and @blur instead of @change
    // would lose the value on the way out.
    //
    // mouse-events-have-key-events (19) and no-static-element-interactions (22)
    // are a real backlog rather than a false alarm — hover-only affordances and
    // clickable divs. Turning either on is a screen-by-screen job, not a config
    // line, and a red build that nobody can make green is how a11y linting gets
    // switched off. Take them one component at a time.

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
        // `{{to.…}}`, not `{{target.…}}`: v7 renders the latter as an empty
        // string, so every message read "pages may not import :" and named
        // neither end of the edge it refused.
        message:
          "{{from.element.type}} may not import {{to.element.type}} this way: FSD allows downward imports only, into another slice through its index.ts barrel, and same-layer only through the target slice's @x door (docs/conventions/PATTERNS.md).",
        policies: [
          // Downward: any layer below, doors included - and into a sliced layer
          // only through that slice's own barrel. The barrel half of the rule
          // was written in PATTERNS.md and enforced nowhere: every deep import
          // into another slice's internals passed lint and type-check while the
          // document said the linter caught it. `fileInternalPath` on the
          // existing policies does the job; `boundaries/entry-point` is
          // deprecated in v7 and rewrites itself into exactly this.
          ...LAYERS.flatMap((layer) =>
            [layer, `${layer}-x`].flatMap((from) =>
              [
                { types: below(layer).filter(isBarreled), entry: SLICE_ENTRY },
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
};
