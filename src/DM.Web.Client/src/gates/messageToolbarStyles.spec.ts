/**
 * @vitest-environment node
 */

/**
 * The message toolbar is drawn from one file: the rules live in the partial
 * _MessageToolbar.sass, the toolbar pulls them in, and neither the toolbar nor
 * the feed around it keeps a copy, since a copy drifts one edit at a time.
 * Read off the compiled stylesheet: a descendant selector reaching the buttons
 * from outside is a rule like any other there, so the rules a file emits on
 * these class names are compared with the rules the mixin emits.
 */
import { describe, expect, it } from "vitest";
import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import {
  compileSass,
  componentCss,
  sassBlocks,
  styleRules,
} from "@/shared/lib/testing/styles";
import { CLIENT_SRC } from "@/shared/lib/testing/sourceTree";

/** The one file that styles the toolbar. */
const PARTIAL = resolve(CLIENT_SRC, "assets/styles/_MessageToolbar.sass");

/** How a page pulls the partial in, and what it includes from it. */
const USE = '@use "@/assets/styles/MessageToolbar" as *';
const INCLUDE = "+message-toolbar";

/**
 * Where the toolbar is rendered: the row of controls over a message of the
 * feed the global chat and the messenger are both drawn with.
 */
const TOOLBAR_FILE = "widgets/chat-feed/MessageToolbar.vue";

/**
 * The feed the toolbar is drawn in. The toolbar's root carries the feed's
 * scope id as well as its own, so a rule of the feed's on a toolbar class
 * reaches the toolbar from outside, and the feed is held to the mixin with it.
 */
const HOST = "widgets/chat-feed/ChatFeed.vue";

/**
 * The toolbar's class names, as the template spells them. The feed positions
 * the toolbar inside its frame in both chats.
 */
const CLASSES = ["toolbar-btn", "msg-toolbar"];

/**
 * A selector that reaches a toolbar element - anywhere in the selector, with
 * whatever modifier suffix follows the name (.toolbar-btn-warn is the same
 * set of buttons).
 */
const TOOLBAR = new RegExp(String.raw`\.(${CLASSES.join("|")})[\w-]*(?![\w-])`);

/** The rules a compiled stylesheet spends on the toolbar's classes. */
const toolbarRules = (css: string): string[] =>
  styleRules(css)
    .filter(({ selector }) => TOOLBAR.test(selector))
    .map(
      ({ selector, declarations }) =>
        `${selector} { ${Object.entries(declarations)
          .map(([property, value]) => `${property}: ${value}`)
          .join("; ")} }`,
    )
    .sort();

describe("the message toolbar", () => {
  // What the partial alone emits: the whole of what a page may carry on these
  // class names, and the yardstick each page below is held to.
  const fromMixin = toolbarRules(compileSass(`${USE}\n${INCLUDE}`));

  it("is styled by a partial of its own", () => {
    expect(existsSync(PARTIAL)).toBe(true);
    expect(readFileSync(PARTIAL, "utf8")).toContain(`=${INCLUDE.slice(1)}`);

    // A yardstick of nothing would let every page pass by emitting nothing
    // either, so the partial has to be seen styling both names first.
    for (const name of CLASSES) {
      expect(
        fromMixin.filter((rule) => rule.startsWith(`.${name}`)),
        name,
      ).not.toEqual([]);
    }
  });

  it(`is pulled into ${TOOLBAR_FILE} from there`, () => {
    const sass = sassBlocks(resolve(CLIENT_SRC, TOOLBAR_FILE));
    expect(sass).toContain(USE);
    expect(sass).toMatch(
      new RegExp(String.raw`^\s*\+message-toolbar\s*$`, "m"),
    );
  });

  it(`is styled from nowhere else in ${TOOLBAR_FILE}`, () => {
    // Every rule the page and the feed around it emit on a toolbar class
    // against every rule the mixin emits. A copy, an override, or a selector
    // of the page's own reaching in from outside is a rule the mixin does not
    // have, and the two lists part.
    expect(
      toolbarRules(
        componentCss(resolve(CLIENT_SRC, TOOLBAR_FILE)) +
          componentCss(resolve(CLIENT_SRC, HOST)),
      ),
    ).toEqual(fromMixin);
  });

  it("draws confirm and cancel in their own ink by sheet order alone", () => {
    // Both refine .toolbar-btn at its weight, so the later rule wins; an
    // !important would hide a reordering that hands the ink back.
    const rules = styleRules(
      compileSass(`${USE}
${INCLUDE}`),
    );
    const at = (selector: string) =>
      rules.findIndex((rule) => rule.selector === selector);
    expect(at(".toolbar-btn")).toBeGreaterThanOrEqual(0);
    for (const selector of [
      ".toolbar-btn-delete-confirm",
      ".toolbar-btn-cancel",
    ]) {
      expect(at(selector), selector).toBeGreaterThan(at(".toolbar-btn"));
      expect(rules[at(selector)].declarations.color, selector).not.toMatch(
        /!important/,
      );
    }
  });

  /** The detector: a pattern that matches nothing would make every file above pass. */
  it("sees a toolbar rule wherever the class name stands in the selector", () => {
    const css = compileSass(
      [
        ".chat-view",
        "  .toolbar-btn",
        "    width: 1px",
        "  &:hover .msg-toolbar",
        "    top: 0",
        "  .sentinel",
        "    top: 0",
      ].join("\n"),
    );

    expect(toolbarRules(css)).toEqual([
      ".chat-view .toolbar-btn { width: 1px }",
      ".chat-view:hover .msg-toolbar { top: 0 }",
    ]);
  });
});
